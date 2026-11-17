using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed partial class VehicleLocationService
{
    private const string RepairLogType = "REPAIR_3D";
    private static readonly HashSet<string> RepairParts = ["door-right-front", "door-left-front", "door-right-rear", "door-left-rear", "tyre-right-front", "tyre-left-front", "tyre-right-rear", "tyre-left-rear", "windshield", "rear-glass", "hood", "trunk", "bumper-front", "bumper-rear", "fender-right-front", "fender-left-front"];

    public async Task ReportRepairAsync(long serviceOrderId, VehicleRepairReportInput input, UserActorContext actor, CancellationToken ct = default)
    {
        if (!RepairParts.Contains(input.PartId ?? "") || string.IsNullOrWhiteSpace(input.Note) || input.Note.Length > 500)
            throw new ArgumentException("Chọn bộ phận và nhập nội dung từ 1 đến 500 ký tự.");
        var strategy = db.Database.CreateExecutionStrategy();
        var attempt = 0;
        await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
                : null;
            var branchId = await RequireTechnicianAsync(actor, ct);
            if (!await AssignedOrders(actor.UserId!.Value).AnyAsync(order => order.Id == serviceOrderId && order.ExecuteBranchId == branchId, ct))
                throw new UnauthorizedAccessException("Xe không thuộc công việc được giao tại chi nhánh của bạn.");
            if (input.Completed)
            {
                var logs = await db.VehicleConditionLogs.AsNoTracking()
                    .Where(log => log.ServiceOrderId == serviceOrderId && log.LogType == RepairLogType)
                    .OrderByDescending(log => log.Id)
                    .Select(log => new { log.Id, log.DefectsAndImages }).ToListAsync(ct);
                var latest = logs.Select(log =>
                {
                    try { return (log.Id, Report: JsonSerializer.Deserialize<VehicleRepairReportInput>(log.DefectsAndImages ?? "null")); }
                    catch (JsonException) { return (log.Id, Report: (VehicleRepairReportInput?)null); }
                }).FirstOrDefault(item => item.Report?.PartId == input.PartId);
                if (input.ExpectedReportId is not > 0 || latest.Id != input.ExpectedReportId || latest.Report == null || latest.Report.Completed)
                    throw new InvalidOperationException("Báo cáo đã thay đổi. Vui lòng tải lại trước khi đánh dấu hoàn tất.");
            }
            db.VehicleConditionLogs.Add(new VehicleConditionLog
            {
                ServiceOrderId = serviceOrderId, LogType = RepairLogType,
                DefectsAndImages = JsonSerializer.Serialize(input with { Note = input.Note.Trim() }),
                InspectorId = actor.UserId, CreatedBy = actor.UserId, CreatedAt = VietnamTime.Now,
            });
            await db.SaveChangesAsync(ct);
            if (transaction != null) await transaction.CommitAsync(ct);
        });
    }

    private async Task<Dictionary<long, IReadOnlyList<VehicleRepairDto>>> ReadRepairsAsync(List<long> orderIds, CancellationToken ct)
    {
        var logs = await db.VehicleConditionLogs.AsNoTracking()
            .Where(log => orderIds.Contains(log.ServiceOrderId) && log.LogType == RepairLogType)
            .OrderByDescending(log => log.Id)
            .Select(log => new { log.Id, log.ServiceOrderId, log.DefectsAndImages, log.CreatedAt, Technician = log.Inspector == null ? "Kỹ thuật viên" : log.Inspector.FullName }).ToListAsync(ct);
        var result = new Dictionary<long, IReadOnlyList<VehicleRepairDto>>();
        foreach (var group in logs.GroupBy(log => log.ServiceOrderId))
        {
            var seen = new HashSet<string>();
            var active = new List<VehicleRepairDto>();
            foreach (var log in group)
            {
                VehicleRepairReportInput? report;
                try { report = JsonSerializer.Deserialize<VehicleRepairReportInput>(log.DefectsAndImages ?? "null"); }
                catch (JsonException) { continue; }
                if (report == null || !RepairParts.Contains(report.PartId ?? "") || !seen.Add(report.PartId!)) continue;
                if (!report.Completed) active.Add(new(report.PartId!, report.Note, log.Technician, log.CreatedAt, log.Id));
            }
            result[group.Key] = active;
        }
        return result;
    }
}
