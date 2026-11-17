using MyGarage.Application.Authorization;
using MyGarage.Application.Bays.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.Exceptions;
using MyGarage.Models;

namespace MyGarage.Application.Bays;

public sealed class BayApplicationService : IBayApplicationService
{
    private readonly IBayQueryStore _queryStore;
    private readonly IBayCommandStore _commandStore;
    private readonly IBayAllocationStore _allocationStore;
    private readonly IBayAuthorizationService _authService;
    private readonly IBayRealtimeNotifier _notifier;

    public BayApplicationService(
        IBayQueryStore queryStore,
        IBayCommandStore commandStore,
        IBayAllocationStore allocationStore,
        IBayAuthorizationService authService,
        IBayRealtimeNotifier notifier)
    {
        _queryStore = queryStore;
        _commandStore = commandStore;
        _allocationStore = allocationStore;
        _authService = authService;
        _notifier = notifier;
    }

    public async Task<IReadOnlyList<BayDetailDto>> GetBaysAsync(
        long? branchId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        var targetBranchId = branchId;
        if (!actor.IsAdmin)
        {
            targetBranchId = actor.BranchId;
        }

        if (!actor.UserId.HasValue || actor.UserId.Value <= 0 ||
            (!actor.IsAdmin && (!targetBranchId.HasValue || targetBranchId.Value <= 0)) ||
            (targetBranchId.HasValue && !await _authService.CanAccessBranchBaysAsync(targetBranchId.Value, actor, ct)))
        {
            return Array.Empty<BayDetailDto>();
        }

        var bays = await _queryStore.GetActiveBaysAsync(targetBranchId, ct);
        var vehicleIds = bays
            .Where(b => b.CurrentOccupiedVehicleId.HasValue)
            .Select(b => b.CurrentOccupiedVehicleId!.Value)
            .Distinct()
            .ToList();

        var vehiclePlates = vehicleIds.Count > 0
            ? await _queryStore.GetVehicleLicensePlatesAsync(vehicleIds, ct)
            : new Dictionary<long, string>();

        return bays.Select(b => new BayDetailDto(
            b.Id,
            b.BranchId,
            b.Code,
            b.Name,
            b.BayType,
            b.ServiceArea,
            b.MaxTonnage,
            b.OperationalStatus ?? "ACTIVE",
            b.CurrentOccupiedVehicleId,
            b.CurrentOccupiedVehicleId.HasValue ? vehiclePlates.GetValueOrDefault(b.CurrentOccupiedVehicleId.Value) : null,
            b.ReservationStatus,
            b.IsActive
        )).ToList();
    }

    public async Task<BayDetailDto?> GetBayByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessBayAsync(id, actor, ct))
        {
            return null;
        }

        var bay = await _queryStore.GetBayWithVehicleAsync(id, ct);
        if (bay == null)
        {
            return null;
        }

        return new BayDetailDto(
            bay.Id,
            bay.BranchId,
            bay.Code,
            bay.Name,
            bay.BayType,
            bay.ServiceArea,
            bay.MaxTonnage,
            bay.OperationalStatus ?? "ACTIVE",
            bay.CurrentOccupiedVehicleId,
            bay.CurrentOccupiedVehicle?.LicensePlate,
            bay.ReservationStatus,
            bay.IsActive
        );
    }

    public async Task<BayDetailDto> CreateBayAsync(
        BayCreateInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAdministerBaysAsync(input.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo khoang sửa chữa cho chi nhánh này.");
        }

        var bay = new Bay
        {
            BranchId = input.BranchId,
            Code = input.Code,
            Name = input.Name,
            BayType = input.BayType ?? "Standard",
            ServiceArea = input.ServiceArea ?? "General",
            MaxTonnage = input.MaxTonnage,
            OperationalStatus = "ACTIVE",
            ReservationStatus = 0,
            IsActive = true,
            IsDeleted = false
        };

        await _commandStore.AddBayAsync(bay, ct);

        return new BayDetailDto(
            bay.Id,
            bay.BranchId,
            bay.Code,
            bay.Name,
            bay.BayType,
            bay.ServiceArea,
            bay.MaxTonnage,
            bay.OperationalStatus,
            null,
            null,
            bay.ReservationStatus,
            bay.IsActive
        );
    }

    public async Task<BayDetailDto?> UpdateBayAsync(
        long id,
        BayUpdateInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessBayAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền chỉnh sửa khoang sửa chữa.");
        }

        var bay = await _queryStore.GetBayForUpdateAsync(id, ct);
        if (bay == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            bay.Name = input.Name;
        }
        if (!string.IsNullOrWhiteSpace(input.BayType))
        {
            bay.BayType = input.BayType;
        }
        if (!string.IsNullOrWhiteSpace(input.ServiceArea))
        {
            bay.ServiceArea = input.ServiceArea;
        }
        if (input.MaxTonnage.HasValue)
        {
            bay.MaxTonnage = input.MaxTonnage.Value;
        }
        if (input.IsActive.HasValue)
        {
            bay.IsActive = input.IsActive.Value;
        }

        BayStatusLog? statusLog = null;
        if (!string.IsNullOrWhiteSpace(input.OperationalStatus))
        {
            var normalizedTarget = BayLifecycleRules.NormalizeStatus(input.OperationalStatus);
            if (!BayLifecycleRules.CanTransitionStatus(bay.OperationalStatus, normalizedTarget))
            {
                throw new InvalidOperationException($"Không thể chuyển trạng thái khoang từ '{bay.OperationalStatus}' sang '{input.OperationalStatus}'.");
            }

            var now = VietnamTime.Now;
            statusLog = new BayStatusLog
            {
                BayId = bay.Id,
                OldStatus = bay.OperationalStatus ?? "ACTIVE",
                NewStatus = normalizedTarget,
                Reason = "Cập nhật qua BayAdmin/Update",
                ChangedBy = actor.UserId.GetValueOrDefault(),
                ChangedAt = now
            };

            bay.OperationalStatus = normalizedTarget;
            bay.LastStatusChangedAt = now;
        }

        await _commandStore.UpdateBayAndStatusLogAsync(statusLog, ct);
        await _notifier.BroadcastBayTimelineUpdatedAsync(bay.BranchId, ct);

        return await GetBayByIdAsync(id, actor, ct);
    }

    public async Task<bool> DeleteBayAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessBayAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xóa khoang sửa chữa.");
        }

        var bay = await _queryStore.GetBayForUpdateAsync(id, ct);
        if (bay == null)
        {
            return false;
        }

        await _commandStore.SoftDeleteBayAsync(bay, ct);
        return true;
    }

    public async Task<BayAllocationResultDto> AllocateBayAsync(
        BayAllocationInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAllocateBayAsync(input.BayId, input.ServiceOrderId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền phân bổ khoang sửa chữa.");
        }

        var bay = await _queryStore.FindBayByIdAsync(input.BayId, ct);
        if (bay == null || bay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang sửa chữa.");
        }
        if (bay.IsActive == false)
        {
            throw new BadRequestException("Khoang đang bị khóa, không thể phân bổ xe.");
        }

        var serviceOrder = await _queryStore.FindServiceOrderByIdAsync(input.ServiceOrderId, ct);
        if (serviceOrder == null)
        {
            throw new NotFoundException("Không tìm thấy lệnh sửa chữa.");
        }
        if (serviceOrder.VehicleId != input.VehicleId)
        {
            throw new BadRequestException("Phương tiện không thuộc lệnh sửa chữa đã chọn.");
        }
        if (serviceOrder.ActualHandover.HasValue ||
            string.Equals(serviceOrder.Status?.Trim(), "CLOSED", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(serviceOrder.Status?.Trim(), "CANCELED", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadRequestException("Không thể phân bổ khoang cho lệnh đã kết thúc hoặc hủy.");
        }

        var vehicle = await _queryStore.FindVehicleByIdAsync(input.VehicleId, ct);
        if (vehicle == null || vehicle.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy phương tiện.");
        }

        var isOccupied = bay.CurrentOccupiedVehicleId.HasValue ||
                         string.Equals(BayLifecycleRules.NormalizeStatus(bay.OperationalStatus ?? string.Empty), "IN_USE", StringComparison.OrdinalIgnoreCase);

        var validation = BayLifecycleRules.ValidateAllocation(new BayLifecycleRules.BayAllocationValidationInput(
            BayBranchId: bay.BranchId,
            ServiceOrderExecuteBranchId: serviceOrder.ExecuteBranchId,
            BayOperationalStatus: bay.OperationalStatus,
            IsCurrentlyOccupied: isOccupied,
            CurrentOccupiedVehicleId: bay.CurrentOccupiedVehicleId,
            BayMaxTonnage: bay.MaxTonnage,
            VehicleTonnage: null
        ));

        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.ErrorMessage ?? "Không thể phân bổ khoang sửa chữa.");
        }

        var now = VietnamTime.Now;
        var allocation = new BayAllocation
        {
            BayId = input.BayId,
            ServiceOrderId = input.ServiceOrderId,
            VehicleId = input.VehicleId,
            AllocatedAt = now,
            AllocatedBy = actor.UserId.GetValueOrDefault(),
            PlannedDuration = input.PlannedDuration,
            Notes = input.Notes
        };

        var statusLog = new BayStatusLog
        {
            BayId = input.BayId,
            OldStatus = bay.OperationalStatus ?? "ACTIVE",
            NewStatus = "IN_USE",
            Reason = $"Allocated to vehicle {vehicle.LicensePlate}",
            ChangedBy = actor.UserId.GetValueOrDefault(),
            ChangedAt = now
        };

        await _allocationStore.AllocateBayInTransactionAsync(bay, allocation, statusLog, input.VehicleId, now, ct);
        await _notifier.BroadcastBayTimelineUpdatedAsync(bay.BranchId, ct);

        var result = await _allocationStore.GetAllocationDetailsAsync(allocation.Id, includeReleasedByStaff: false, ct);
        return MapToResultDto(result);
    }

    public async Task<BayAllocationResultDto> ReleaseBayAsync(
        long bayId,
        string? notes,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanReleaseBayAsync(bayId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền giải phóng khoang sửa chữa.");
        }

        var bay = await _queryStore.FindBayByIdAsync(bayId, ct);
        if (bay == null || bay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang sửa chữa.");
        }

        var allocation = await _allocationStore.GetActiveAllocationForBayAsync(bayId, ct);
        if (!BayLifecycleRules.CanReleaseBay(bay.OperationalStatus, allocation != null))
        {
            throw new BadRequestException("Khoang không có phân bổ hoạt động để giải phóng.");
        }

        var now = VietnamTime.Now;
        allocation!.ReleasedAt = now;
        allocation.ReleasedBy = actor.UserId;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            allocation.Notes = string.IsNullOrEmpty(allocation.Notes)
                ? notes
                : $"{allocation.Notes}\nRelease notes: {notes}";
        }

        var statusLog = new BayStatusLog
        {
            BayId = bayId,
            OldStatus = bay.OperationalStatus ?? "IN_USE",
            NewStatus = "ACTIVE",
            Reason = "Vehicle released",
            ChangedBy = actor.UserId.GetValueOrDefault(),
            ChangedAt = now
        };

        await _allocationStore.ReleaseBayInTransactionAsync(bay, statusLog, now, ct);
        await _notifier.BroadcastBayTimelineUpdatedAsync(bay.BranchId, ct);

        var result = await _allocationStore.GetAllocationDetailsAsync(allocation.Id, includeReleasedByStaff: true, ct);
        return MapToResultDto(result);
    }

    public async Task<BayAllocationResultDto> TransferBayAsync(
        long fromBayId,
        long toBayId,
        long vehicleId,
        string? notes,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanTransferBayAsync(fromBayId, toBayId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền điều chuyển phương tiện giữa các khoang.");
        }

        var sourceBay = await _queryStore.FindBayByIdAsync(fromBayId, ct);
        if (sourceBay == null || sourceBay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang gốc.");
        }

        var targetBay = await _queryStore.FindBayByIdAsync(toBayId, ct);
        if (targetBay == null || targetBay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang đích.");
        }
        if (targetBay.IsActive == false)
        {
            throw new BadRequestException("Khoang đích đang bị khóa, không thể điều chuyển xe.");
        }

        var sourceAllocation = await _allocationStore.GetActiveAllocationForBayAsync(fromBayId, ct);
        if (sourceAllocation == null || sourceAllocation.VehicleId != vehicleId)
        {
            throw new BadRequestException("Không tìm thấy phân bổ đang hoạt động cho phương tiện tại khoang gốc.");
        }

        var isTargetOccupied = targetBay.CurrentOccupiedVehicleId.HasValue ||
                               string.Equals(BayLifecycleRules.NormalizeStatus(targetBay.OperationalStatus ?? string.Empty), "IN_USE", StringComparison.OrdinalIgnoreCase);

        var validation = BayLifecycleRules.ValidateTransfer(new BayLifecycleRules.BayTransferInput(
            SourceBayBranchId: sourceBay.BranchId,
            TargetBayBranchId: targetBay.BranchId,
            VehicleId: vehicleId,
            SourceCurrentVehicleId: sourceBay.CurrentOccupiedVehicleId,
            TargetBayOperationalStatus: targetBay.OperationalStatus,
            IsTargetOccupied: isTargetOccupied,
            TargetBayMaxTonnage: targetBay.MaxTonnage,
            VehicleTonnage: null
        ));

        if (!validation.IsValid)
        {
            throw new BadRequestException(validation.ErrorMessage ?? "Không thể điều chuyển phương tiện sang khoang đích.");
        }

        var now = VietnamTime.Now;
        sourceAllocation.ReleasedAt = now;
        sourceAllocation.ReleasedBy = actor.UserId;
        sourceAllocation.Notes = string.IsNullOrEmpty(sourceAllocation.Notes)
            ? $"Transferred to bay {targetBay.Code}"
            : $"{sourceAllocation.Notes}\nTransferred to bay {targetBay.Code}";

        var targetAllocation = new BayAllocation
        {
            BayId = toBayId,
            ServiceOrderId = sourceAllocation.ServiceOrderId,
            VehicleId = vehicleId,
            AllocatedAt = now,
            AllocatedBy = actor.UserId.GetValueOrDefault(),
            PlannedDuration = sourceAllocation.PlannedDuration,
            Notes = notes
        };

        var sourceLog = new BayStatusLog
        {
            BayId = fromBayId,
            OldStatus = sourceBay.OperationalStatus ?? "IN_USE",
            NewStatus = "ACTIVE",
            Reason = $"Transferred vehicle {vehicleId} to bay {targetBay.Code}",
            ChangedBy = actor.UserId.GetValueOrDefault(),
            ChangedAt = now
        };

        var targetLog = new BayStatusLog
        {
            BayId = toBayId,
            OldStatus = targetBay.OperationalStatus ?? "ACTIVE",
            NewStatus = "IN_USE",
            Reason = $"Received vehicle {vehicleId} from bay {sourceBay.Code}",
            ChangedBy = actor.UserId.GetValueOrDefault(),
            ChangedAt = now
        };

        await _allocationStore.TransferBayInTransactionAsync(
            sourceBay,
            targetBay,
            targetAllocation,
            sourceLog,
            targetLog,
            vehicleId,
            now,
            ct);

        await _notifier.BroadcastBayTimelineUpdatedAsync(sourceBay.BranchId, ct);

        var result = await _allocationStore.GetAllocationDetailsAsync(targetAllocation.Id, includeReleasedByStaff: false, ct);
        return MapToResultDto(result);
    }

    public async Task<IReadOnlyList<BayTimelineItemDto>> GetBayTimelineAsync(
        DateTime date,
        long branchId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessBranchBaysAsync(branchId, actor, ct))
        {
            return Array.Empty<BayTimelineItemDto>();
        }

        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var bays = await _queryStore.GetActiveBaysAsync(branchId, ct);
        var allocations = await _allocationStore.GetTimelineAllocationsAsync(branchId, startOfDay, endOfDay, ct);

        var now = VietnamTime.Now;
        var timeline = new List<BayTimelineItemDto>();

        foreach (var bay in bays)
        {
            var bayAllocations = allocations
                .Where(a => a.BayId == bay.Id)
                .Select(a => new BayTimelineAllocationDto(
                    a.Id,
                    a.BayId,
                    a.ServiceOrderId,
                    a.ServiceOrder?.Code ?? string.Empty,
                    a.VehicleId,
                    a.Vehicle?.LicensePlate ?? string.Empty,
                    a.AllocatedAt,
                    a.ReleasedAt,
                    a.PlannedDuration,
                    a.ReleasedAt.HasValue
                        ? (int)(a.ReleasedAt.Value - a.AllocatedAt).TotalMinutes
                        : (int)(now - a.AllocatedAt).TotalMinutes
                ))
                .ToList();

            timeline.Add(new BayTimelineItemDto(
                bay.Id,
                bay.Name,
                bay.ServiceArea ?? string.Empty,
                bay.OperationalStatus ?? "ACTIVE",
                bayAllocations
            ));
        }

        return timeline;
    }

    public async Task<BayDetailDto> UpdateBayStatusAsync(
        long bayId,
        string status,
        string? reason,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessBayAsync(bayId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền thay đổi trạng thái khoang sửa chữa.");
        }

        var bay = await _queryStore.GetBayForUpdateAsync(bayId, ct);
        if (bay == null)
        {
            throw new NotFoundException("Không tìm thấy khoang sửa chữa.");
        }

        var normalizedTarget = BayLifecycleRules.NormalizeStatus(status);
        if (!BayLifecycleRules.CanTransitionStatus(bay.OperationalStatus, normalizedTarget))
        {
            throw new InvalidOperationException($"Không thể chuyển trạng thái khoang từ '{bay.OperationalStatus}' sang '{status}'.");
        }

        var now = VietnamTime.Now;
        var statusLog = new BayStatusLog
        {
            BayId = bay.Id,
            OldStatus = bay.OperationalStatus ?? "ACTIVE",
            NewStatus = normalizedTarget,
            Reason = reason ?? "Cập nhật trạng thái thủ công",
            ChangedBy = actor.UserId.GetValueOrDefault(),
            ChangedAt = now
        };

        await _commandStore.UpdateBayStatusInTransactionAsync(bay, statusLog, normalizedTarget, now, ct);
        await _notifier.BroadcastBayTimelineUpdatedAsync(bay.BranchId, ct);

        return (await GetBayByIdAsync(bayId, actor, ct))!;
    }

    private static BayAllocationResultDto MapToResultDto(BayAllocation a)
    {
        var now = VietnamTime.Now;
        var actualDuration = a.ReleasedAt.HasValue
            ? (int)(a.ReleasedAt.Value - a.AllocatedAt).TotalMinutes
            : (int)(now - a.AllocatedAt).TotalMinutes;

        return new BayAllocationResultDto(
            a.Id,
            a.BayId,
            a.Bay?.Name ?? string.Empty,
            a.ServiceOrderId,
            a.ServiceOrder?.Code ?? string.Empty,
            a.VehicleId,
            a.Vehicle?.LicensePlate ?? string.Empty,
            a.AllocatedAt,
            a.AllocatedBy,
            a.AllocatedByStaff?.FullName,
            a.ReleasedAt,
            a.ReleasedBy,
            a.ReleasedByStaff?.FullName,
            a.PlannedDuration,
            actualDuration,
            a.Notes,
            a.CreatedAt
        );
    }
}
