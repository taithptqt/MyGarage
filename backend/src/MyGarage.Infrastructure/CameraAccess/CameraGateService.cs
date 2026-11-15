using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.CameraAccess;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.CameraAccess;

public record GateActor(long Id, string Role, long? BranchId);
public class GateDeniedException : Exception;
public class GateConflictException(string message) : Exception(message);

public partial class CameraGateService(GarageContext db, IDataProtectionProvider protection,
    IHubContext<CameraGateHub> hub, IServiceOrderService orders, IHttpClientFactory clients)
{
    private readonly IDataProtector protector = protection.CreateProtector("MyGarage.CameraStreams.v1");
    public void Scope(GateActor actor, long branch, bool manage = false, bool confirm = false)
    {
        if (actor.Id <= 0 || !CameraAccessRules.CanAccessBranch(actor.Role, actor.BranchId, branch) ||
            (manage && !CameraAccessRules.CanManage(actor.Role)) ||
            (confirm && !CameraAccessRules.CanConfirm(actor.Role))) throw new GateDeniedException();
    }
    private IQueryable<CameraDevice> Cameras(GateActor actor, long? branch)
    {
        if (actor.Id <= 0 || !CameraAccessRules.CanRead(actor.Role)) throw new GateDeniedException();
        if (branch.HasValue) Scope(actor, branch.Value);
        if (actor.Role != "ADMIN" && !actor.BranchId.HasValue) throw new GateDeniedException();
        var scope = actor.Role == "ADMIN" ? branch : actor.BranchId;
        return db.CameraDevices.Where(x => !scope.HasValue || x.BranchId == scope);
    }
    public async Task<object> ListCameras(GateActor actor, long? branch)
    {
        var rows = await Cameras(actor, branch).AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        return rows.Select(x => new { x.Id, x.BranchId, x.Name, x.Lane, x.Direction, x.Enabled,
            x.MinConfidence, x.RetentionDays, x.LastHeartbeatAt,
            health = x.LastHeartbeatAt < DateTime.UtcNow.AddSeconds(-45) || x.LastHeartbeatAt == null ? "OFFLINE" : x.Health,
            streamUrl = string.IsNullOrEmpty(x.EncryptedStreamUrl) ? "" : "rtsp://********",
            x.UpdatedBy, x.UpdatedAt });
    }
    public async Task<object> SaveCamera(GateActor actor, long? id, CameraSave command)
    {
        Scope(actor, command.BranchId, manage: true);
        if (string.IsNullOrWhiteSpace(command.Name) || string.IsNullOrWhiteSpace(command.Lane)) throw new ArgumentException("Cần tên camera và làn xe.");
        if (!await db.Branches.AnyAsync(x => x.Id == command.BranchId && x.IsDeleted != true)) throw new ArgumentException("Chi nhánh không hợp lệ.");
        var camera = id.HasValue ? await db.CameraDevices.FindAsync(id.Value) : new CameraDevice();
        if (camera == null) throw new KeyNotFoundException();
        if (id.HasValue) { Scope(actor, camera.BranchId, manage: true); if(camera.BranchId != command.BranchId) throw new ArgumentException("Không thể chuyển camera sang chi nhánh khác."); }
        if (id.HasValue && camera.Direction != command.Direction && await db.VehicleAccessEvents.AnyAsync(x=>x.CameraId==id))
            throw new ArgumentException("Camera đã có lịch sử; hãy tạo camera mới khi đổi hướng làn.");
        if (command.StreamUrl != null)
        {
            if (!Uri.TryCreate(command.StreamUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("rtsp" or "rtsps" or "localvideo"))
                throw new ArgumentException("Nguồn camera phải là RTSP, RTSPS hoặc localvideo://sample.");
            camera.EncryptedStreamUrl = protector.Protect(command.StreamUrl);
        }
        if (string.IsNullOrEmpty(camera.EncryptedStreamUrl)) throw new ArgumentException("Cần địa chỉ nguồn cho camera mới.");
        var before = $"{camera.Name}|{camera.Lane}|{camera.Direction}|{camera.Enabled}|{camera.MinConfidence}";
        camera.BranchId = command.BranchId;
        camera.Name = command.Name.Trim(); camera.Lane = command.Lane.Trim(); camera.Direction = command.Direction;
        camera.MinConfidence = command.MinConfidence; camera.Enabled = command.Enabled;
        camera.RetentionDays = command.RetentionDays; camera.UpdatedAt = DateTime.UtcNow; camera.UpdatedBy = actor.Id;
        string? credential = null;
        if (!id.HasValue) { credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); camera.CredentialHash = Hash(credential); db.CameraDevices.Add(camera); }
        db.VehicleAccessAudits.Add(new() { BranchId = camera.BranchId, CameraId = id, ActorId = actor.Id,
            Action = id.HasValue ? "CAMERA_UPDATE" : "CAMERA_CREATE", Before = before,
            After = $"{camera.Name}|{camera.Lane}|{camera.Direction}|{camera.Enabled}|{camera.MinConfidence}", At = DateTime.UtcNow });
        await db.SaveChangesAsync(); await Notify(camera.BranchId);
        return new { camera.Id, credential };
    }
    public async Task<object> RotateCredential(GateActor actor, long id)
    {
        var camera = await db.CameraDevices.FindAsync(id) ?? throw new KeyNotFoundException();
        Scope(actor, camera.BranchId, manage: true);
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); camera.CredentialHash = Hash(secret);
        db.VehicleAccessAudits.Add(new() { BranchId = camera.BranchId, CameraId = id, ActorId = actor.Id,
            Action = "CREDENTIAL_ROTATE", At = DateTime.UtcNow });
        await db.SaveChangesAsync(); return new { credential = secret };
    }
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public async Task<CameraDevice> Device(long id, string secret)
    {
        var camera = await db.CameraDevices.FindAsync(id);
        if (camera == null || !camera.Enabled || string.IsNullOrEmpty(secret) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(secret)), Encoding.ASCII.GetBytes(camera.CredentialHash)))
            throw new GateDeniedException();
        return camera;
    }
    public async Task<object> Configuration(long id, string secret)
    {
        var camera = await Device(id, secret);
        return new { camera.Id, camera.Direction, camera.MinConfidence, streamUrl = protector.Unprotect(camera.EncryptedStreamUrl) };
    }
    public async Task Heartbeat(long id, string secret, string status)
    {
        var camera = await Device(id, secret);
        camera.LastHeartbeatAt = DateTime.UtcNow;
        camera.Health = status is "ONLINE" or "RECONNECTING" or "ERROR" or "VIDEO_FINISHED" ? status : "ERROR";
        await db.SaveChangesAsync(); await Notify(camera.BranchId);
    }
    private static byte[]? Evidence(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded)) return null;
        if (encoded.Length > 2800000) throw new ArgumentException("Ảnh vượt giới hạn 2 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); } catch(FormatException) { throw new ArgumentException("Ảnh không hợp lệ."); }
        if (bytes.Length > 2_000_000 || bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[^2] != 0xff || bytes[^1] != 0xd9)
            throw new ArgumentException("Chỉ nhận bằng chứng JPEG hợp lệ, tối đa 2 MB.");
        return bytes;
    }
    public async Task<object> Observe(long cameraId, string secret, CameraObservation command)
    {
        var camera = await Device(cameraId, secret);
        return await Ingest(camera, command);
    }
    internal async Task<object> Ingest(CameraDevice camera, CameraObservation command)
    {
        var cameraId = camera.Id;
        if (command.EventKey == Guid.Empty) throw new ArgumentException("Thiếu mã sự kiện.");
        var found = await db.VehicleAccessEvents.AsNoTracking().FirstOrDefaultAsync(x => x.CameraId == cameraId && x.EventKey == command.EventKey);
        if (found != null) return new { found.Id, duplicate = true };
        if (command.ObservedAt > DateTimeOffset.UtcNow.AddMinutes(5)) throw new ArgumentException("Thời gian sự kiện ở tương lai.");
        var row = new VehicleAccessEvent { CameraId = cameraId, BranchId = camera.BranchId, EventKey = command.EventKey,
            WorkerInstance = command.WorkerInstance, Direction = camera.Direction, RawPlate = command.Plate,
            Plate = CameraAccessRules.NormalizePlate(command.Plate), Confidence = command.Confidence,
            ObservedAt = command.ObservedAt.UtcDateTime, ReceivedAt = DateTime.UtcNow,
            Snapshot = Evidence(command.SnapshotBase64), PlateCrop = Evidence(command.CropBase64) };
        await Match(row);
        if (row.Direction == "EXIT") row.Status = "EXIT_PENDING";
        if (row.Plate.Length < 7 || row.Confidence < camera.MinConfidence) row.Status = "NEEDS_REVIEW";
        db.VehicleAccessEvents.Add(row);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            found = await db.VehicleAccessEvents.AsNoTracking().FirstOrDefaultAsync(x => x.CameraId == cameraId && x.EventKey == command.EventKey);
            if (found == null) throw;
            return new { found.Id, duplicate = true };
        }
        await Notify(row.BranchId); return new { row.Id, duplicate = false };
    }
    private async Task Match(VehicleAccessEvent row)
    {
        row.VehicleId = null; row.AppointmentId = null; row.ServiceOrderId = null;
        if (row.Plate.Length < 7) return;
        var vehicles = await db.Vehicles.Where(v => v.IsDeleted != true && v.LicensePlate.ToUpper().Replace("-", "").Replace(".", "").Replace(" ", "") == row.Plate)
            .Select(v => v.Id).Take(2).ToListAsync();
        if (vehicles.Count != 1) return;
        row.VehicleId = vehicles[0];
        var localDay = row.ObservedAt.AddHours(7).Date;
        var appointments = await db.Appointments.Where(a => a.BranchId == row.BranchId && a.VehicleId == row.VehicleId &&
            a.StartTime >= localDay && a.StartTime < localDay.AddDays(1) && a.Status != "CANCELED")
            .Select(a => a.Id).Take(2).ToListAsync();
        if(appointments.Count == 1) row.AppointmentId = appointments[0];
        var candidates = await db.ServiceOrders.Where(s => s.ExecuteBranchId == row.BranchId && s.VehicleId == row.VehicleId && s.Status != "CANCELED")
            .OrderByDescending(s => s.Id).Select(s => s.Id).Take(2).ToListAsync();
        // Never silently choose between concurrent open jobs; an explicit Gate Pass resolves exits.
        if(candidates.Count == 1) row.ServiceOrderId = candidates[0];
    }
    private IQueryable<VehicleAccessEvent> Events(GateActor actor, CameraFilter filter)
    {
        var ids = Cameras(actor, filter.BranchId).Select(x => x.Id);
        var query = db.VehicleAccessEvents.Where(x => ids.Contains(x.CameraId));
        if(filter.CameraId.HasValue) query = query.Where(x => x.CameraId == filter.CameraId);
        if(!string.IsNullOrEmpty(filter.Direction)) query = query.Where(x => x.Direction == filter.Direction);
        if(!string.IsNullOrEmpty(filter.Status)) query = query.Where(x => x.Status == filter.Status);
        if(!string.IsNullOrEmpty(filter.Plate)) { var plate=CameraAccessRules.NormalizePlate(filter.Plate); query=query.Where(x=>x.Plate.Contains(plate)); }
        if(filter.From.HasValue) query=query.Where(x=>x.ObservedAt>=filter.From);
        if(filter.To.HasValue) query=query.Where(x=>x.ObservedAt<=filter.To);
        return query;
    }
    public async Task<object> List(GateActor actor, CameraFilter filter)
    {
        var query = Events(actor, filter).AsNoTracking();
        var page=Math.Max(1,filter.Page); var size=Math.Clamp(filter.PageSize,1,100);
        var items=await query.OrderByDescending(x=>x.ReceivedAt).Skip((page-1)*size).Take(size)
            .Select(x=>new { x.Id,x.CameraId,x.BranchId,x.Direction,x.RawPlate,x.Plate,x.Confidence,x.ObservedAt,x.Status,
                x.VehicleId,x.AppointmentId,x.ServiceOrderId,x.GatePassId,x.ReviewedBy,x.ReviewedAt,x.Notes,
                reviewedByName=db.Staffs.Where(s=>s.Id==x.ReviewedBy).Select(s=>s.FullName).FirstOrDefault(),
                hasSnapshot=x.Snapshot!=null,hasCrop=x.PlateCrop!=null }).ToListAsync();
        return new { items,total=await query.CountAsync(),page,pageSize=size };
    }
    public async Task<VehicleAccessEvent> Event(GateActor actor,long id)
    {
        var row=await db.VehicleAccessEvents.FindAsync(id)??throw new KeyNotFoundException();
        Scope(actor,row.BranchId); return row;
    }
    public async Task<object> Detail(GateActor actor,long id,long? passId = null)
    {
        var row=await Event(actor,id);
        var reason= row.Direction=="EXIT" ? await ExitReason(row,passId) : null;
        var audits=await db.VehicleAccessAudits.Where(x=>x.EventId==id).OrderBy(x=>x.Id)
            .Select(x=>new {x.Action,x.ActorId,x.At,x.Before,x.After,x.Reason,
                actorName=db.Staffs.Where(s=>s.Id==x.ActorId).Select(s=>s.FullName).FirstOrDefault()}).ToListAsync();
        var passes=row.VehicleId.HasValue ? await db.GatePasses.Where(g=>g.ServiceOrder.VehicleId==row.VehicleId &&
            g.ServiceOrder.ExecuteBranchId==row.BranchId && g.PassedAt==null && (g.Status=="APPROVED"||g.Status=="ISSUED"))
            .Select(g=>new {g.Id,g.ServiceOrderId,g.Status,g.ExpiresAtUtc}).ToListAsync() : null;
        return new {row.Id,reason,passes,audits};
    }
    private async Task<string?> ExitReason(VehicleAccessEvent row,long? passId)
    {
        var pass=passId.HasValue ? await db.GatePasses.Include(x=>x.ServiceOrder).SingleOrDefaultAsync(x=>x.Id==passId) : null;
        if(pass==null) return CameraAccessRules.ExitBlock(row.VehicleId.HasValue,false,false,false);
        var matched=pass.ServiceOrder.VehicleId==row.VehicleId && pass.ServiceOrder.ExecuteBranchId==row.BranchId;
        var ready=matched ? await orders.GetHandoverReadinessAsync(pass.ServiceOrderId) : null;
        return CameraAccessRules.ExitBlock(matched,CameraAccessRules.PassUsable(pass.Status,pass.PassedAt,pass.ExpiresAtUtc,DateTime.UtcNow),
            ready?.CanHandover==true,pass.IsOldPartsChecked==true);
    }
    public async Task Review(GateActor actor,long id,CameraReview command)
    {
        var strategy=db.Database.CreateExecutionStrategy();
        long branch=0;
        await strategy.ExecuteAsync(async () => {
            db.ChangeTracker.Clear();
            await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var row=await Event(actor,id); branch=row.BranchId;
            if(row.Status is "CONFIRMED" or "REJECTED") throw new GateConflictException("Sự kiện đã được xử lý. Hãy tải lại.");
            var before=$"{row.Plate}|{row.Status}|{row.GatePassId}";
            switch(command.Action)
            {
                case "CORRECT":
                    if(string.IsNullOrWhiteSpace(command.Plate)||string.IsNullOrWhiteSpace(command.Reason)) throw new ArgumentException("Nhập biển số và lý do sửa.");
                    row.Plate=CameraAccessRules.NormalizePlate(command.Plate);
                    if(row.Plate.Length<7) throw new ArgumentException("Biển số chưa đủ ký tự.");
                    await Match(row); row.Status="NEEDS_REVIEW";
                    break;
                case "REJECT":
                    Scope(actor,row.BranchId,confirm:true);
                    if(string.IsNullOrWhiteSpace(command.Reason)) throw new ArgumentException("Cần lý do từ chối.");
                    row.Status="REJECTED"; break;
                case "CONFIRM":
                    Scope(actor,row.BranchId,confirm:true);
                    if(row.Plate.Length<7) throw new ArgumentException("Cần xác nhận lại biển số.");
                    if(command.Override && (!CameraAccessRules.CanManage(actor.Role)||string.IsNullOrWhiteSpace(command.Reason))) throw new GateDeniedException();
                    if(row.Direction=="EXIT")
                    {
                        if(command.OldPartsChecked && command.GatePassId.HasValue)
                        {
                            var selectedPass=await db.GatePasses.Include(p=>p.ServiceOrder).SingleOrDefaultAsync(p=>p.Id==command.GatePassId);
                            if(selectedPass != null && selectedPass.ServiceOrder.VehicleId==row.VehicleId && selectedPass.ServiceOrder.ExecuteBranchId==row.BranchId)
                                selectedPass.IsOldPartsChecked=true;
                        }
                        var reason=await ExitReason(row,command.GatePassId);
                        if(reason!=null && !command.Override) throw new GateConflictException(reason);
                        if(reason==null && command.GatePassId.HasValue)
                        {
                            var pass=await db.GatePasses.SingleAsync(x=>x.Id==command.GatePassId);
                            row.GatePassId=pass.Id; row.ServiceOrderId=pass.ServiceOrderId;
                            pass.Status="SCANNED"; pass.PassedAt=MyGarage.Common.VietnamTime.Now;
                            pass.SecurityCheckedAt=pass.PassedAt; pass.ScannedBy=actor.Id;
                        }
                        else if(command.Override) row.Notes="[MANAGER_OVERRIDE] "+reason;
                    }
                    row.Status="CONFIRMED"; break;
                case "NOTE":
                    if(string.IsNullOrWhiteSpace(command.Reason)) throw new ArgumentException("Cần nội dung ghi chú.");
                    break;
                default: throw new ArgumentException("Thao tác không hợp lệ.");
            }
            if(!string.IsNullOrEmpty(command.Reason)) row.Notes=(row.Notes+" "+command.Reason).Trim()[..Math.Min(1000,(row.Notes+" "+command.Reason).Trim().Length)];
            row.ReviewedBy=actor.Id; row.ReviewedAt=DateTime.UtcNow;
            db.VehicleAccessAudits.Add(new() {BranchId=row.BranchId,EventId=id,ActorId=actor.Id,
                Action=command.Override?"MANAGER_OVERRIDE":command.Action,Before=before,After=$"{row.Plate}|{row.Status}|{row.GatePassId}",Reason=command.Reason??"",At=DateTime.UtcNow});
            await db.SaveChangesAsync(); await tx.CommitAsync();
        });
        await Notify(branch);
    }
    public async Task<string> Export(GateActor actor,CameraFilter filter)
    {
        if(!CameraAccessRules.CanManage(actor.Role)) throw new GateDeniedException();
        var query=Events(actor,filter);
        if(await query.CountAsync()>10000) throw new ArgumentException("Thu hẹp bộ lọc xuống tối đa 10.000 sự kiện.");
        var rows=await query.OrderByDescending(x=>x.ObservedAt).Select(x=>new {x.Id,x.BranchId,x.CameraId,x.Direction,x.Plate,x.Status,x.ObservedAt,x.ReviewedBy}).ToListAsync();
        var csv=new StringBuilder("Id,Branch,Camera,Direction,Plate,Status,ObservedUtc,ReviewedBy\r\n");
        foreach(var x in rows) csv.AppendLine($"{x.Id},{x.BranchId},{x.CameraId},{x.Direction},{x.Plate},{x.Status},{x.ObservedAt:O},{x.ReviewedBy}");
        db.AuditLogs.Add(new AuditLog {UserId=actor.Id,UserType="STAFF",BranchId=null,
            Action="GATE_EXPORT",TableName="VehicleAccessEvents",RecordId=0,
            NewValue=$"Count={rows.Count};RequestedBranch={filter.BranchId};ActorBranch={actor.BranchId}",Timestamp=MyGarage.Common.VietnamTime.Now});
        await db.SaveChangesAsync(); return csv.ToString();
    }
    private Task Notify(long branch)=>hub.Clients.Groups($"gate-{branch}","gate-admin").SendAsync("GateChanged",new {branchId=branch});
}
