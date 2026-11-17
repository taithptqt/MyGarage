using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Interfaces.Stores.Common;
using MyGarage.Models;

namespace MyGarage.Application.Bays;

public sealed class BayAdminManagementService : IBayAdminManagementService
{
    private readonly IBayAdminWriteDataService _dataService;
    private readonly IAppConfigFileStore _configStore;

    public BayAdminManagementService(IBayAdminWriteDataService dataService, IAppConfigFileStore configStore)
    {
        _dataService = dataService;
        _configStore = configStore;
    }

    public async Task<BayAdminCommandResult> CreateBayAsync(
        BayUpsertRequest? request,
        long? currentUserId,
        CancellationToken ct = default)
    {
        if (request == null) return BayAdminCommandResult.BadRequest("Payload is required.");
        if (request.BranchId <= 0) return BayAdminCommandResult.BadRequest("BranchId is required.");
        if (string.IsNullOrWhiteSpace(request.Code)) return BayAdminCommandResult.BadRequest("Bay code is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return BayAdminCommandResult.BadRequest("Bay name is required.");

        var branchExists = await _dataService.BranchExistsAsync(request.BranchId, ct);
        if (!branchExists) return BayAdminCommandResult.BadRequest("Branch does not exist.");

        var normalizedCode = request.Code.Trim();
        var duplicatedCode = await _dataService.BayCodeExistsAsync(normalizedCode, ct: ct);
        if (duplicatedCode) return BayAdminCommandResult.BadRequest("Bay code already exists.");

        var sequenceOrder = request.SequenceOrder.GetValueOrDefault();
        if (sequenceOrder <= 0)
        {
            sequenceOrder = await _dataService.GetNextSequenceOrderAsync(request.BranchId, ct: ct);
        }

        var duplicatedSequence = await _dataService.BaySequenceOrderExistsAsync(request.BranchId, sequenceOrder, ct: ct);
        if (duplicatedSequence)
        {
            return BayAdminCommandResult.BadRequest("Sequence order already exists in this branch.");
        }

        var now = VietnamTime.Now;
        var bay = new Bay
        {
            BranchId = request.BranchId,
            SequenceOrder = sequenceOrder,
            Code = normalizedCode,
            Name = request.Name.Trim(),
            BayType = string.IsNullOrWhiteSpace(request.BayType) ? "GENERAL" : request.BayType.Trim(),
            MaxTonnage = request.MaxTonnage,
            OperationalStatus = BayAdminSettingsHelper.NormalizeOperationalStatus(request.OperationalStatus),
            LastStatusChangedAt = now,
            ReservationStatus = BayAdminConstants.ReservationStatusAvailable,
            IsActive = request.IsActive ?? true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId
        };

        await _dataService.AddBayAsync(bay, ct);

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        settings.BayMetadata[bay.Id] = new BayMetadata
        {
            Area = BayAdminSettingsHelper.NormalizeArea(request.Area),
            Capacity = BayAdminSettingsHelper.NormalizeCapacity(request.Capacity),
            SupportedSegments = BayAdminSettingsHelper.NormalizeSegments(request.SupportedSegments)
        };

        await BayAdminSettingsHelper.WriteSettingsAsync(_configStore, settings, ct);

        return BayAdminCommandResult.Ok(new
        {
            message = "Bay created successfully.",
            id = bay.Id,
            sequenceOrder = bay.SequenceOrder
        });
    }

    public async Task<BayAdminCommandResult> UpdateBayAsync(
        long id,
        BayUpsertRequest request,
        long? currentUserId,
        CancellationToken ct = default)
    {
        var bay = await _dataService.GetBayByIdAsync(id, ct);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        if (request.BranchId <= 0) return BayAdminCommandResult.BadRequest("BranchId is required.");
        if (string.IsNullOrWhiteSpace(request.Code)) return BayAdminCommandResult.BadRequest("Bay code is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return BayAdminCommandResult.BadRequest("Bay name is required.");

        var duplicatedCode = await _dataService.BayCodeExistsAsync(request.Code.Trim(), id, ct);
        if (duplicatedCode) return BayAdminCommandResult.BadRequest("Bay code already exists.");

        var branchExists = await _dataService.BranchExistsAsync(request.BranchId, ct);
        if (!branchExists) return BayAdminCommandResult.BadRequest("Branch does not exist.");

        var targetSequenceOrder = request.SequenceOrder.HasValue && request.SequenceOrder.Value > 0
            ? request.SequenceOrder.Value
            : bay.SequenceOrder > 0
                ? bay.SequenceOrder
                : await _dataService.GetNextSequenceOrderAsync(request.BranchId, id, ct);

        if (request.BranchId != bay.BranchId &&
            (!request.SequenceOrder.HasValue || request.SequenceOrder.Value <= 0))
        {
            targetSequenceOrder = await _dataService.GetNextSequenceOrderAsync(request.BranchId, id, ct);
        }

        var duplicatedSequence = await _dataService.BaySequenceOrderExistsAsync(request.BranchId, targetSequenceOrder, id, ct);
        if (duplicatedSequence)
        {
            return BayAdminCommandResult.BadRequest("Sequence order already exists in this branch.");
        }

        bay.BranchId = request.BranchId;
        bay.SequenceOrder = targetSequenceOrder;
        bay.Code = request.Code.Trim();
        bay.Name = request.Name.Trim();
        bay.BayType = string.IsNullOrWhiteSpace(request.BayType) ? "GENERAL" : request.BayType.Trim();
        bay.MaxTonnage = request.MaxTonnage;
        var normalizedStatus = BayAdminSettingsHelper.NormalizeOperationalStatus(request.OperationalStatus);
        var previousStatus = BayAdminSettingsHelper.NormalizeOperationalStatus(bay.OperationalStatus);
        bay.OperationalStatus = normalizedStatus;
        if (!string.Equals(previousStatus, normalizedStatus, StringComparison.OrdinalIgnoreCase))
        {
            bay.LastStatusChangedAt = VietnamTime.Now;
        }
        bay.IsActive = request.IsActive ?? bay.IsActive;
        bay.UpdatedAt = VietnamTime.Now;
        bay.UpdatedBy = currentUserId;

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        settings.BayMetadata[id] = new BayMetadata
        {
            Area = BayAdminSettingsHelper.NormalizeArea(request.Area),
            Capacity = BayAdminSettingsHelper.NormalizeCapacity(request.Capacity),
            SupportedSegments = BayAdminSettingsHelper.NormalizeSegments(request.SupportedSegments)
        };

        await _dataService.SaveChangesInTransactionAsync(ct);
        await BayAdminSettingsHelper.WriteSettingsAsync(_configStore, settings, ct);

        return BayAdminCommandResult.Ok(new
        {
            message = "Bay updated successfully.",
            sequenceOrder = bay.SequenceOrder
        });
    }

    public async Task<BayAdminCommandResult> HideBayAsync(long id, long? currentUserId, CancellationToken ct = default)
    {
        var bay = await _dataService.GetBayByIdAsync(id, ct);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        var now = VietnamTime.Now;

        bay.IsDeleted = true;
        bay.IsActive = false;
        bay.ReservationStatus = BayAdminConstants.ReservationStatusAvailable;
        bay.LastStatusChangedAt = now;
        bay.DeletedAt = now;
        bay.DeletedBy = currentUserId;
        bay.UpdatedAt = now;
        bay.UpdatedBy = currentUserId;

        await _dataService.SaveChangesInTransactionAsync(ct);

        return BayAdminCommandResult.Ok(new { message = "Bay hidden successfully." });
    }

    public async Task<BayAdminCommandResult> LockBayAsync(long id, long? currentUserId, CancellationToken ct = default)
    {
        var bay = await _dataService.GetBayByIdAsync(id, ct);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        bay.IsActive = false;
        bay.LastStatusChangedAt = VietnamTime.Now;
        bay.UpdatedAt = VietnamTime.Now;
        bay.UpdatedBy = currentUserId;
        await _dataService.SaveChangesInTransactionAsync(ct);

        return BayAdminCommandResult.Ok(new { message = "Bay locked successfully." });
    }

    public async Task<BayAdminCommandResult> UnlockBayAsync(long id, long? currentUserId, CancellationToken ct = default)
    {
        var bay = await _dataService.GetBayByIdAsync(id, ct);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        bay.IsActive = true;
        bay.LastStatusChangedAt = VietnamTime.Now;
        bay.UpdatedAt = VietnamTime.Now;
        bay.UpdatedBy = currentUserId;
        await _dataService.SaveChangesInTransactionAsync(ct);

        return BayAdminCommandResult.Ok(new { message = "Bay unlocked successfully." });
    }

    public async Task<BayAdminCommandResult> UpdateBayStatusAsync(
        long id,
        UpdateBayStatusRequest request,
        long? currentUserId,
        CancellationToken ct = default)
    {
        var bay = await _dataService.GetBayByIdAsync(id, ct);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        var normalizedStatus = BayAdminSettingsHelper.NormalizeOperationalStatus(request.Status);
        if (!BayAdminSettingsHelper.IsKnownOperationalStatus(normalizedStatus))
        {
            return BayAdminCommandResult.BadRequest("Trạng thái khoang không hợp lệ.");
        }

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(_configStore, ct);
        var activeSessions = settings.Sessions.Where(s => s.BayId == id && s.ReleasedAt == null).ToList();
        var activeVehicleCount = activeSessions.Select(s => s.VehicleId).Distinct().Count();
        if (bay.CurrentOccupiedVehicleId.HasValue)
        {
            activeVehicleCount = activeSessions.Select(s => s.VehicleId).Append(bay.CurrentOccupiedVehicleId.Value).Distinct().Count();
        }

        if (normalizedStatus != BayAdminConstants.StatusActive && activeVehicleCount > 0)
        {
            return BayAdminCommandResult.BadRequest("Khoang vẫn đang có xe, không thể chuyển sang trạng thái khác ACTIVE.");
        }

        bay.OperationalStatus = normalizedStatus;
        bay.LastStatusChangedAt = VietnamTime.Now;
        if (normalizedStatus == BayAdminConstants.StatusEmpty)
        {
            bay.ReservationStatus = BayAdminConstants.ReservationStatusAvailable;
        }
        else if (normalizedStatus == BayAdminConstants.StatusInUse && bay.CurrentOccupiedVehicleId.HasValue)
        {
            bay.ReservationStatus = BayAdminConstants.ReservationStatusOccupied;
        }
        bay.UpdatedAt = VietnamTime.Now;
        bay.UpdatedBy = currentUserId;
        await _dataService.SaveChangesInTransactionAsync(ct);

        return BayAdminCommandResult.Ok(new { message = "Bay status updated successfully." });
    }
}
