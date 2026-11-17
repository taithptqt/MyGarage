using System.Data;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed partial class VehicleLocationService(IGarageContext db, IBayOccupancySnapshotSource? occupancySource = null) : IVehicleLocationService
{
    private static readonly string[] TerminalStatuses = ["CLOSED", "CANCELED", "CANCELLED", "DELIVERED"];
    private static readonly SemaphoreSlim ReportLock = new(1, 1);
    private static string LogReason(string value) => value.Length <= 500 ? value : value[..500];
    private Task<BayOccupancySnapshot>? occupancyTask;
    private Task<BayOccupancySnapshot> ReadOccupancyAsync(CancellationToken ct) => occupancyTask ??= occupancySource?.ReadAsync(ct) ?? Task.FromResult(new BayOccupancySnapshot([], new Dictionary<long, int>()));
    private IQueryable<ServiceOrder> OpenOrders => db.ServiceOrders.Where(order => !TerminalStatuses.Contains(order.Status) && order.ActualHandover == null);

    private IQueryable<ServiceOrder> AssignedOrders(long technicianId) => OpenOrders.Where(order =>
        db.ServiceStaffAssignments.Any(assignment => assignment.TechnicianId == technicianId && assignment.ServiceOrderDetail.ServiceOrderId == order.Id &&
            assignment.ServiceOrderDetail.Status != "CANCELED" && assignment.ServiceOrderDetail.Status != "DECLINED" &&
            assignment.Status != "CANCELED" && assignment.Status != "CANCELLED" && assignment.Status != "REJECTED") ||
        db.DiagnosisAssignments.Any(assignment => assignment.Diagnosis.ServiceOrderId == order.Id &&
            (assignment.TechnicianId == technicianId || assignment.AssistantTechnicianId == technicianId) &&
            assignment.Status != "CANCELED" && assignment.Status != "CANCELLED" && assignment.CancellationStatus != "APPROVED" &&
            !db.DiagnosisAssignments.Any(later => later.DiagnosisId == assignment.DiagnosisId && later.Id > assignment.Id)));

    private async Task<long> RequireTechnicianAsync(UserActorContext actor, CancellationToken ct)
    {
        if (actor.UserId is not > 0 || !string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actor.RoleCode, "TECH", StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Chỉ kỹ thuật viên được báo vị trí xe được giao.");
        var branch = await db.Staffs.AsNoTracking().Where(staff => staff.Id == actor.UserId && staff.IsDeleted != true).Select(staff => staff.BranchId).FirstOrDefaultAsync(ct);
        if (!branch.HasValue || actor.BranchId != branch || !await db.Branches.AnyAsync(item => item.Id == branch.Value && item.IsDeleted != true, ct)) throw new UnauthorizedAccessException("Không xác định được chi nhánh kỹ thuật viên.");
        return branch.Value;
    }

    private sealed record Position(long VehicleId, long BayId, long BranchId, string Code, string Name, string BayType, string Area, DateTime? At, long? By, string? Note);

    private async Task<Dictionary<long, Position>> PositionsAsync(List<long> vehicleIds, CancellationToken ct, long? customerId = null)
    {
        var snapshot = await ReadOccupancyAsync(ct);
        var allocations = await db.BayAllocations.AsNoTracking()
            .Where(allocation => vehicleIds.Contains(allocation.VehicleId) && allocation.ReleasedAt == null && allocation.Bay.IsDeleted != true &&
                allocation.ServiceOrder.VehicleId == allocation.VehicleId && allocation.ServiceOrder.ExecuteBranchId == allocation.Bay.BranchId &&
                !TerminalStatuses.Contains(allocation.ServiceOrder.Status) && allocation.ServiceOrder.ActualHandover == null && (!customerId.HasValue || allocation.ServiceOrder.CustomerId == customerId.Value))
            .Select(allocation => new Position(allocation.VehicleId, allocation.BayId, allocation.Bay.BranchId, allocation.Bay.Code, allocation.Bay.Name,
                allocation.Bay.BayType, allocation.Bay.ServiceArea, allocation.AllocatedAt,
                allocation.AllocatedBy, allocation.Notes)).ToListAsync(ct);
        // Legacy bay pointers and file sessions do not identify the owning order.
        // They cannot safely establish which customer may see the location or note.
        if (customerId.HasValue)
        {
            var allocationBayIds = allocations.Select(position => position.BayId).Distinct().ToList();
            var bayPointers = await db.Bays.AsNoTracking().Where(bay => allocationBayIds.Contains(bay.Id))
                .Select(bay => new { bay.Id, bay.CurrentOccupiedVehicleId, bay.LastStatusChangedAt })
                .ToDictionaryAsync(bay => bay.Id, ct);
            return allocations.Where(position => position.At.HasValue && !snapshot.Sessions.Any(session =>
                    session.VehicleId == position.VehicleId &&
                    (session.ReleasedAt ?? session.AssignedAt) > position.At.Value &&
                    (session.BayId != position.BayId || session.ReleasedAt.HasValue)) &&
                (!bayPointers.TryGetValue(position.BayId, out var bay) || !bay.LastStatusChangedAt.HasValue ||
                 bay.LastStatusChangedAt <= position.At || bay.CurrentOccupiedVehicleId == position.VehicleId ||
                 snapshot.Sessions.Any(session => session.VehicleId == position.VehicleId && session.BayId == position.BayId &&
                     session.ReleasedAt == null && session.AssignedAt >= bay.LastStatusChangedAt)))
                .GroupBy(position => position.VehicleId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(position => position.At).First());
        }
        var occupied = await db.Bays.AsNoTracking().Where(bay => bay.IsDeleted != true && bay.CurrentOccupiedVehicleId.HasValue && vehicleIds.Contains(bay.CurrentOccupiedVehicleId.Value))
            .Select(bay => new Position(bay.CurrentOccupiedVehicleId!.Value, bay.Id, bay.BranchId, bay.Code, bay.Name, bay.BayType, bay.ServiceArea,
                bay.LastStatusChangedAt, bay.UpdatedBy, null)).ToListAsync(ct);
        var sessionBayIds = snapshot.Sessions.Where(session => vehicleIds.Contains(session.VehicleId) && session.ReleasedAt == null).Select(session => session.BayId).Distinct().ToList();
        var sessionBays = await db.Bays.AsNoTracking().Where(bay => sessionBayIds.Contains(bay.Id) && bay.IsDeleted != true).ToDictionaryAsync(bay => bay.Id, ct);
        var legacy = snapshot.Sessions.Where(session => vehicleIds.Contains(session.VehicleId) && session.ReleasedAt == null && sessionBays.ContainsKey(session.BayId)).Select(session =>
        {
            var bay = sessionBays[session.BayId];
            return new Position(session.VehicleId, bay.Id, bay.BranchId, bay.Code, bay.Name, bay.BayType, bay.ServiceArea, session.AssignedAt, null, session.Note);
        });
        var handovers = await db.ServiceOrders.AsNoTracking().Where(order => vehicleIds.Contains(order.VehicleId) && order.ActualHandover.HasValue)
            .GroupBy(order => order.VehicleId).Select(group => new { VehicleId = group.Key, At = group.Max(order => order.ActualHandover) }).ToDictionaryAsync(row => row.VehicleId, row => row.At, ct);
        return allocations.Concat(occupied.Where(position => !allocations.Any(allocation => allocation.VehicleId == position.VehicleId && allocation.BayId == position.BayId) &&
                !snapshot.Sessions.Any(session => session.VehicleId == position.VehicleId && session.BayId == position.BayId && session.ReleasedAt == null))).Concat(legacy)
            .Where(position => !handovers.TryGetValue(position.VehicleId, out var handover) || (position.At.HasValue && position.At > handover))
            .Where(position => !snapshot.Sessions.Any(session => session.VehicleId == position.VehicleId && session.BayId == position.BayId && session.ReleasedAt.HasValue && session.ReleasedAt > position.At))
            .GroupBy(position => position.VehicleId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(position => position.At).ThenByDescending(position => position.Note != null).First());
    }

    public async Task<CustomerGarageDto> GetCustomerGarageAsync(long customerId, CancellationToken ct = default)
    {
        var owned = await db.CustomerVehicles.AsNoTracking().Where(link => link.CustomerId == customerId && link.IsDeleted != true && link.IsCurrentOwner == true && link.Vehicle.IsDeleted != true)
            .Select(link => new { link.VehicleId, link.Vehicle.LicensePlate, ModelLabel = link.Vehicle.Model.Brand.Name + " " + link.Vehicle.Model.ModelName }).Distinct().ToListAsync(ct);
        var vehicleIds = owned.Select(vehicle => vehicle.VehicleId).ToList();
        var positions = await PositionsAsync(vehicleIds, ct, customerId);
        var orders = await OpenOrders.AsNoTracking().Where(order => order.CustomerId == customerId && vehicleIds.Contains(order.VehicleId))
            .OrderByDescending(order => order.Id).Select(order => new { order.Id, order.VehicleId, order.ExecuteBranchId }).ToListAsync(ct);
        var branchIds = positions.Values.Select(position => position.BranchId).Concat(orders.Select(order => order.ExecuteBranchId)).Distinct().ToList();
        var branches = await db.Branches.AsNoTracking().Where(branch => branchIds.Contains(branch.Id) && branch.IsDeleted != true).OrderBy(branch => branch.Name).Select(branch => new { branch.Id, branch.Name }).ToListAsync(ct);
        var validBranchIds = branches.Select(branch => branch.Id).ToList();
        var bays = await db.Bays.AsNoTracking().Where(bay => validBranchIds.Contains(bay.BranchId) && bay.IsDeleted != true)
            .OrderBy(bay => bay.SequenceOrder).ThenBy(bay => bay.Code)
            .Select(bay => new GarageBayDto(bay.Id, bay.BranchId, bay.Code, bay.Name, bay.BayType, bay.ServiceArea)).ToListAsync(ct);
        var reporterIds = positions.Values.Where(position => position.By.HasValue).Select(position => position.By!.Value).Distinct().ToList();
        var reporters = await db.Staffs.AsNoTracking().Where(staff => reporterIds.Contains(staff.Id)).ToDictionaryAsync(staff => staff.Id, staff => staff.FullName, ct);
        var repairs = await ReadRepairsAsync(orders.Select(order => order.Id).ToList(), ct);
        var vehicles = owned.Select(vehicle =>
        {
            positions.TryGetValue(vehicle.VehicleId, out var position);
            var order = orders.FirstOrDefault(order => order.VehicleId == vehicle.VehicleId);
            var branchId = position?.BranchId ?? order?.ExecuteBranchId;
            var branch = branches.FirstOrDefault(branch => branch.Id == branchId);
            if (branch == null || (position != null && !bays.Any(bay => bay.Id == position.BayId && bay.BranchId == position.BranchId))) position = null;
            return new GarageVehicleDto(vehicle.VehicleId, vehicle.LicensePlate, vehicle.ModelLabel, order?.Id,
                branch?.Id, branch?.Name, position?.BayId, position?.Code, position?.Name, position?.BayType, position?.Area,
                position?.At, position?.By is long by ? reporters.GetValueOrDefault(by) : null, position?.Note) { Repairs = order == null ? [] : repairs.GetValueOrDefault(order.Id, []) };
        }).ToList();
        return new CustomerGarageDto(branches.Where(branch => vehicles.Any(vehicle => vehicle.BranchId == branch.Id)).Select(branch => new GarageBranchDto(branch.Id, branch.Name, bays.Where(bay => bay.BranchId == branch.Id).ToList())).ToList(), vehicles);
    }

    public async Task<TechnicianLocationWorkspaceDto> GetTechnicianWorkspaceAsync(UserActorContext actor, CancellationToken ct = default)
    {
        var branchId = await RequireTechnicianAsync(actor, ct);
        var orders = await AssignedOrders(actor.UserId!.Value).AsNoTracking().Where(order => order.ExecuteBranchId == branchId)
            .OrderByDescending(order => order.Id).Select(order => new { order.Id, order.VehicleId, order.Code, order.Vehicle.LicensePlate }).ToListAsync(ct);
        var positions = await PositionsAsync(orders.Select(order => order.VehicleId).Distinct().ToList(), ct);
        var snapshot = await ReadOccupancyAsync(ct);
        var bayRows = await db.Bays.AsNoTracking().Where(bay => bay.BranchId == branchId && bay.IsDeleted != true && bay.IsActive != false &&
                (bay.OperationalStatus == "ACTIVE" || bay.OperationalStatus == "EMPTY" || bay.OperationalStatus == "IN_USE" || bay.OperationalStatus == null))
            .OrderBy(bay => bay.SequenceOrder).ThenBy(bay => bay.Code)
            .Select(bay => new { bay.Id, bay.BranchId, bay.Code, bay.Name, bay.BayType, bay.ServiceArea }).ToListAsync(ct);
        var occupiedIds = await OccupiedVehicleIdsAsync(branchId, ct);
        var occupancy = await PositionsAsync(occupiedIds, ct);
        var bays = bayRows.Select(bay => new LocationBayOptionDto(bay.Id, bay.BranchId, bay.Code, bay.Name, bay.BayType, bay.ServiceArea,
            occupancy.Values.Where(position => position.BayId == bay.Id).Select(position => position.VehicleId).ToList(), snapshot.Capacities.GetValueOrDefault(bay.Id, 1))).ToList();
        var repairs = await ReadRepairsAsync(orders.Select(order => order.Id).ToList(), ct);
        return new TechnicianLocationWorkspaceDto(orders.Select(order => new LocationOrderDto(order.Id, order.VehicleId, order.Code, order.LicensePlate, branchId,
            positions.GetValueOrDefault(order.VehicleId)?.BayId, positions.GetValueOrDefault(order.VehicleId)?.Code) { Repairs = repairs.GetValueOrDefault(order.Id, []) }).ToList(), bays);
    }

    public async Task ReportAsync(long serviceOrderId, VehicleLocationReportInput input, UserActorContext actor, CancellationToken ct = default)
    {
        var branchId = await RequireTechnicianAsync(actor, ct);
        if (input.BayId <= 0 || string.IsNullOrWhiteSpace(input.Note) || input.Note.Length > 500) throw new ArgumentException("Chọn vị trí và nhập ghi chú từ 1 đến 500 ký tự.");
        await ReportLock.WaitAsync(ct);
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            var attempt = 0;
            await strategy.ExecuteAsync(async () =>
            {
                if (attempt++ > 0) { db.ChangeTracker.Clear(); occupancyTask = null; }
                await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
                var order = await AssignedOrders(actor.UserId!.Value).FirstOrDefaultAsync(order => order.Id == serviceOrderId && order.ExecuteBranchId == branchId, ct)
                    ?? throw new UnauthorizedAccessException("Xe không thuộc công việc được giao tại chi nhánh của bạn.");
                var target = await db.Bays.FirstOrDefaultAsync(bay => bay.Id == input.BayId && bay.BranchId == branchId && bay.IsDeleted != true && bay.IsActive != false, ct)
                    ?? throw new ArgumentException("Vị trí không thuộc chi nhánh hoặc đã ngừng hoạt động.");
                if (target.OperationalStatus is not (null or "ACTIVE" or "EMPTY" or "IN_USE")) throw new InvalidOperationException("Vị trí đang khóa hoặc bảo trì.");
                var positions = await PositionsAsync([order.VehicleId], ct);
                var current = positions.GetValueOrDefault(order.VehicleId);
                if (current?.BayId != input.ExpectedBayId) throw new InvalidOperationException("Vị trí vừa được cập nhật. Hãy tải lại trước khi báo vị trí mới.");
                if (current != null && current.BranchId != branchId) throw new InvalidOperationException("Xe đang ở chi nhánh khác; cần xử lý bàn giao chi nhánh trước.");
                var snapshot = await ReadOccupancyAsync(ct);
                var occupancy = await PositionsAsync(await OccupiedVehicleIdsAsync(branchId, ct), ct);
                var targetOtherCount = occupancy.Values.Count(position => position.BayId == target.Id && position.VehicleId != order.VehicleId);
                if (targetOtherCount >= snapshot.Capacities.GetValueOrDefault(target.Id, 1)) throw new InvalidOperationException("Vị trí đã đủ số xe cho phép. Hãy chọn khoang hoặc bãi còn chỗ.");
                var now = VietnamTime.Now;
                var active = await db.BayAllocations.Where(allocation => allocation.VehicleId == order.VehicleId && allocation.Bay.BranchId == branchId && allocation.ReleasedAt == null).ToListAsync(ct);
                foreach (var allocation in active) { allocation.ReleasedAt = now; allocation.ReleasedBy = actor.UserId; }
                var sources = await db.Bays.Where(bay => bay.BranchId == branchId && bay.CurrentOccupiedVehicleId == order.VehicleId && bay.Id != target.Id).ToListAsync(ct);
                foreach (var source in sources)
                {
                    var remaining = occupancy.Values.Where(position => position.BayId == source.Id && position.VehicleId != order.VehicleId).Select(position => (long?)position.VehicleId).FirstOrDefault();
                    var sourceOperational = source.OperationalStatus is "BROKEN" or "UNDER_MAINTENANCE" ? source.OperationalStatus : "ACTIVE";
                    db.BayStatusLogs.Add(new BayStatusLog { BayId = source.Id, OldStatus = source.OperationalStatus ?? "ACTIVE", NewStatus = sourceOperational, Reason = LogReason($"Chuyển xe sang {target.Code}: {input.Note.Trim()}"), ChangedBy = actor.UserId.Value, ChangedAt = now });
                    source.CurrentOccupiedVehicleId = remaining; source.OperationalStatus = sourceOperational;
                    source.ReservationStatus = remaining.HasValue ? 2 : 0; source.LastStatusChangedAt = now; source.UpdatedAt = now; source.UpdatedBy = actor.UserId;
                }
                db.BayStatusLogs.Add(new BayStatusLog { BayId = target.Id, OldStatus = target.OperationalStatus ?? "ACTIVE", NewStatus = "ACTIVE", Reason = LogReason($"KTV báo vị trí: {input.Note.Trim()}"), ChangedBy = actor.UserId.Value, ChangedAt = now });
                if (targetOtherCount == 0 || target.CurrentOccupiedVehicleId == null) target.CurrentOccupiedVehicleId = order.VehicleId;
                target.OperationalStatus = "ACTIVE"; target.ReservationStatus = 2;
                target.LastStatusChangedAt = now; target.UpdatedAt = now; target.UpdatedBy = actor.UserId;
                db.BayAllocations.Add(new BayAllocation { BayId = target.Id, ServiceOrderId = order.Id, VehicleId = order.VehicleId, AllocatedBy = actor.UserId.Value, AllocatedAt = now, Notes = input.Note.Trim(), CreatedAt = now });
                await db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);
            });
        }
        finally { ReportLock.Release(); }
    }

    private async Task<List<long>> OccupiedVehicleIdsAsync(long branchId, CancellationToken ct)
    {
        var snapshot = await ReadOccupancyAsync(ct);
        var bayIds = await db.Bays.Where(bay => bay.BranchId == branchId && bay.IsDeleted != true).Select(bay => bay.Id).ToListAsync(ct);
        var pointers = await db.Bays.Where(bay => bay.BranchId == branchId && bay.CurrentOccupiedVehicleId.HasValue && bay.IsDeleted != true).Select(bay => bay.CurrentOccupiedVehicleId!.Value).ToListAsync(ct);
        var allocated = await db.BayAllocations.Where(allocation => bayIds.Contains(allocation.BayId) && allocation.ReleasedAt == null).Select(allocation => allocation.VehicleId).ToListAsync(ct);
        return pointers.Concat(allocated).Concat(snapshot.Sessions.Where(session => session.ReleasedAt == null && bayIds.Contains(session.BayId)).Select(session => session.VehicleId)).Distinct().ToList();
    }
}
