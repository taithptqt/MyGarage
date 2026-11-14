using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Application.Bays.DTOs;
using MyGarage.Domain.Rules;
using MyGarage.Exceptions;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class BayApplicationService : IBayApplicationService
{
    private readonly IGarageContext _context;
    private readonly IBayAuthorizationService _authService;
    private readonly IHubContext<ChatHub> _hubContext;

    public BayApplicationService(
        IGarageContext context,
        IBayAuthorizationService authService,
        IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _authService = authService;
        _hubContext = hubContext;
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

        var query = _context.Bays.AsNoTracking().Where(b => b.IsDeleted != true);
        if (targetBranchId.HasValue)
        {
            query = query.Where(b => b.BranchId == targetBranchId.Value);
        }

        var bays = await query.OrderBy(b => b.Id).ToListAsync(ct);
        var vehicleIds = bays
            .Where(b => b.CurrentOccupiedVehicleId.HasValue)
            .Select(b => b.CurrentOccupiedVehicleId!.Value)
            .Distinct()
            .ToList();

        var vehiclePlates = vehicleIds.Count > 0
            ? await _context.Vehicles.AsNoTracking()
                .Where(v => vehicleIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, v => v.LicensePlate, ct)
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

        var bay = await _context.Bays.AsNoTracking()
            .Include(b => b.CurrentOccupiedVehicle)
            .FirstOrDefaultAsync(b => b.Id == id && b.IsDeleted != true, ct);

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

        await _context.Bays.AddAsync(bay, ct);
        await _context.SaveChangesAsync(ct);

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

        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id && b.IsDeleted != true, ct);
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

        if (!string.IsNullOrWhiteSpace(input.OperationalStatus))
        {
            var normalizedTarget = BayLifecycleRules.NormalizeStatus(input.OperationalStatus);
            if (!BayLifecycleRules.CanTransitionStatus(bay.OperationalStatus, normalizedTarget))
            {
                throw new InvalidOperationException($"Không thể chuyển trạng thái khoang từ '{bay.OperationalStatus}' sang '{input.OperationalStatus}'.");
            }

            var now = VietnamTime.Now;
            var statusLog = new BayStatusLog
            {
                BayId = bay.Id,
                OldStatus = bay.OperationalStatus ?? "ACTIVE",
                NewStatus = normalizedTarget,
                Reason = "Cập nhật qua BayAdmin/Update",
                ChangedBy = actor.UserId.GetValueOrDefault(),
                ChangedAt = now
            };
            await _context.BayStatusLogs.AddAsync(statusLog, ct);

            bay.OperationalStatus = normalizedTarget;
            bay.LastStatusChangedAt = now;
        }

        await _context.SaveChangesAsync(ct);
        await BroadcastBayTimelineUpdatedAsync(bay.BranchId);

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

        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == id && b.IsDeleted != true, ct);
        if (bay == null)
        {
            return false;
        }

        bay.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
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

        var bay = await _context.Bays.FindAsync(new object[] { input.BayId }, ct);
        if (bay == null || bay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang sửa chữa.");
        }

        var serviceOrder = await _context.ServiceOrders.FindAsync(new object[] { input.ServiceOrderId }, ct);
        if (serviceOrder == null)
        {
            throw new NotFoundException("Không tìm thấy lệnh sửa chữa.");
        }

        var vehicle = await _context.Vehicles.FindAsync(new object[] { input.VehicleId }, ct);
        if (vehicle == null)
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayAllocations.AddAsync(allocation, ct);
            await _context.BayStatusLogs.AddAsync(statusLog, ct);

            bay.OperationalStatus = "IN_USE";
            bay.CurrentOccupiedVehicleId = input.VehicleId;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });

        await BroadcastBayTimelineUpdatedAsync(bay.BranchId);

        var result = await _context.BayAllocations.AsNoTracking()
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Include(a => a.AllocatedByStaff)
            .FirstAsync(a => a.Id == allocation.Id, ct);

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

        var bay = await _context.Bays.FindAsync(new object[] { bayId }, ct);
        if (bay == null || bay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang sửa chữa.");
        }

        var allocation = await _context.BayAllocations
            .Where(a => a.BayId == bayId && a.ReleasedAt == null)
            .OrderByDescending(a => a.AllocatedAt)
            .FirstOrDefaultAsync(ct);

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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayStatusLogs.AddAsync(statusLog, ct);

            bay.OperationalStatus = "ACTIVE";
            bay.CurrentOccupiedVehicleId = null;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });

        await BroadcastBayTimelineUpdatedAsync(bay.BranchId);

        var result = await _context.BayAllocations.AsNoTracking()
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Include(a => a.AllocatedByStaff)
            .Include(a => a.ReleasedByStaff)
            .FirstAsync(a => a.Id == allocation.Id, ct);

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

        var sourceBay = await _context.Bays.FindAsync(new object[] { fromBayId }, ct);
        if (sourceBay == null || sourceBay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang gốc.");
        }

        var targetBay = await _context.Bays.FindAsync(new object[] { toBayId }, ct);
        if (targetBay == null || targetBay.IsDeleted == true)
        {
            throw new NotFoundException("Không tìm thấy khoang đích.");
        }

        var sourceAllocation = await _context.BayAllocations
            .Where(a => a.BayId == fromBayId && a.VehicleId == vehicleId && a.ReleasedAt == null)
            .OrderByDescending(a => a.AllocatedAt)
            .FirstOrDefaultAsync(ct);

        if (sourceAllocation == null)
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayAllocations.AddAsync(targetAllocation, ct);
            await _context.BayStatusLogs.AddAsync(sourceLog, ct);
            await _context.BayStatusLogs.AddAsync(targetLog, ct);

            sourceBay.OperationalStatus = "ACTIVE";
            sourceBay.CurrentOccupiedVehicleId = null;
            sourceBay.LastStatusChangedAt = now;

            targetBay.OperationalStatus = "IN_USE";
            targetBay.CurrentOccupiedVehicleId = vehicleId;
            targetBay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });

        await BroadcastBayTimelineUpdatedAsync(sourceBay.BranchId);

        var result = await _context.BayAllocations.AsNoTracking()
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Include(a => a.AllocatedByStaff)
            .FirstAsync(a => a.Id == targetAllocation.Id, ct);

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

        var bays = await _context.Bays.AsNoTracking()
            .Where(b => b.BranchId == branchId && b.IsDeleted != true)
            .OrderBy(b => b.Id)
            .ToListAsync(ct);

        var allocations = await _context.BayAllocations.AsNoTracking()
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Where(a => a.Bay != null && a.Bay.BranchId == branchId &&
                        a.AllocatedAt < endOfDay &&
                        (a.ReleasedAt == null || a.ReleasedAt >= startOfDay))
            .ToListAsync(ct);

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

        var bay = await _context.Bays.FirstOrDefaultAsync(b => b.Id == bayId && b.IsDeleted != true, ct);
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayStatusLogs.AddAsync(statusLog, ct);

            bay.OperationalStatus = normalizedTarget;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });

        await BroadcastBayTimelineUpdatedAsync(bay.BranchId);

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

    private async Task BroadcastBayTimelineUpdatedAsync(long branchId)
    {
        if (branchId <= 0)
        {
            return;
        }

        await _hubContext.Clients
            .Group($"WorkshopBranch_{branchId}")
            .SendAsync("BayTimelineUpdated", new
            {
                branchId,
                changedAt = VietnamTime.Now
            });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }
}
