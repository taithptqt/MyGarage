using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Application.Bays;

public sealed class BayAdminVehicleAssignmentService : IBayAdminVehicleAssignmentService
{
    private readonly IBayAdminWriteDataService _writeDataService;
    private readonly IBayVehicleLookupDataService _vehicleLookupDataService;

    public BayAdminVehicleAssignmentService(
        IBayAdminWriteDataService writeDataService,
        IBayVehicleLookupDataService vehicleLookupDataService)
    {
        _writeDataService = writeDataService;
        _vehicleLookupDataService = vehicleLookupDataService;
    }

    public async Task<BayAdminCommandResult> AssignVehicleAsync(
        long id,
        AssignVehicleRequest request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
    {
        if (request.VehicleId <= 0) return BayAdminCommandResult.BadRequest("VehicleId is required.");

        var bay = await _writeDataService.GetBayByIdAsync(id, ct);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        var vehicle = await _vehicleLookupDataService.LoadVehicleDescriptorAsync(request.VehicleId, ct);
        if (vehicle == null) return BayAdminCommandResult.BadRequest("Không tìm thấy xe cần gán.");

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(contentRootPath);
        var metadata = BayAdminSettingsHelper.ResolveMetadata(settings, bay.Id);
        var now = VietnamTime.Now;

        var status = BayAdminSettingsHelper.NormalizeOperationalStatus(bay.OperationalStatus);
        var invalidStateMessage = BayAdminSettingsHelper.ValidateBayForAssign(bay, status);
        if (!string.IsNullOrWhiteSpace(invalidStateMessage))
        {
            return BayAdminCommandResult.BadRequest(invalidStateMessage);
        }

        var activeSessionsInBay = settings.Sessions
            .Where(s => s.BayId == bay.Id && s.ReleasedAt == null)
            .ToList();

        var activeVehicleSet = activeSessionsInBay
            .Select(s => s.VehicleId)
            .ToHashSet();
        if (bay.CurrentOccupiedVehicleId.HasValue)
        {
            activeVehicleSet.Add(bay.CurrentOccupiedVehicleId.Value);
        }

        if (activeVehicleSet.Count >= metadata.Capacity)
        {
            return BayAdminCommandResult.BadRequest("Khoang đã đầy, không thể gán thêm xe.");
        }

        var normalizedSegment = BayAdminSettingsHelper.NormalizeSegment(vehicle.Segment);
        if (!BayAdminSettingsHelper.IsSegmentCompatible(normalizedSegment, metadata.SupportedSegments))
        {
            return BayAdminCommandResult.BadRequest("Khoang không phù hợp với loại xe này.");
        }

        var activeBayId = await BayAdminRowBuilder.FindVehicleActiveBayAsync(_vehicleLookupDataService, request.VehicleId, settings, ignoreBayId: bay.Id, ct: ct);
        if (activeBayId.HasValue)
        {
            return BayAdminCommandResult.BadRequest(
                $"Xe đang nằm trong khoang #{activeBayId.Value}. Hãy điều phối thay vì gán mới.");
        }

        settings.Sessions.Add(new BaySession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            BayId = bay.Id,
            VehicleId = request.VehicleId,
            AssignedAt = now,
            ReleasedAt = null,
            ServiceStatus = request.ServiceStatus?.Trim(),
            Note = request.Note?.Trim()
        });

        if (!bay.CurrentOccupiedVehicleId.HasValue)
        {
            bay.CurrentOccupiedVehicleId = request.VehicleId;
        }

        if (status == BayAdminConstants.StatusEmpty)
        {
            bay.OperationalStatus = BayAdminConstants.StatusInUse;
        }

        bay.ReservationStatus = BayAdminConstants.ReservationStatusOccupied;
        bay.LastStatusChangedAt = now;
        bay.UpdatedAt = now;
        bay.UpdatedBy = currentUserId;

        await _writeDataService.SaveChangesInTransactionAsync(ct);
        await BayAdminSettingsHelper.WriteSettingsAsync(contentRootPath, settings);

        return BayAdminCommandResult.Ok(new { message = "Vehicle assigned to bay successfully." });
    }

    public async Task<BayAdminCommandResult> RemoveVehicleFromBayAsync(
        long id,
        RemoveVehicleRequest request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
    {
        var bay = await _writeDataService.GetBayByIdAsync(id, ct);
        if (bay == null) return BayAdminCommandResult.NotFound("Khoang không tồn tại.");

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(contentRootPath);
        var now = VietnamTime.Now;

        var activeSessions = settings.Sessions
            .Where(s => s.BayId == id && s.ReleasedAt == null)
            .ToList();

        var targetSessions = request.VehicleId.HasValue
            ? activeSessions.Where(s => s.VehicleId == request.VehicleId.Value).ToList()
            : activeSessions;

        foreach (var session in targetSessions)
        {
            session.ReleasedAt = now;
            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                session.Note = request.Note.Trim();
            }
        }

        var shouldReleaseCurrent = !request.VehicleId.HasValue ||
                                   (bay.CurrentOccupiedVehicleId.HasValue && bay.CurrentOccupiedVehicleId == request.VehicleId.Value);
        if (shouldReleaseCurrent && bay.CurrentOccupiedVehicleId.HasValue)
        {
            bay.CurrentOccupiedVehicleId = null;
        }

        var remainingVehicleIds = settings.Sessions
            .Where(s => s.BayId == id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .Distinct()
            .ToList();

        if (remainingVehicleIds.Count > 0)
        {
            bay.CurrentOccupiedVehicleId = remainingVehicleIds[0];
            bay.ReservationStatus = BayAdminConstants.ReservationStatusOccupied;
        }
        else
        {
            bay.CurrentOccupiedVehicleId = null;
            bay.ReservationStatus = BayAdminConstants.ReservationStatusAvailable;
            if (BayAdminSettingsHelper.NormalizeOperationalStatus(bay.OperationalStatus) == BayAdminConstants.StatusInUse)
            {
                bay.OperationalStatus = BayAdminConstants.StatusActive;
            }
        }

        bay.LastStatusChangedAt = now;
        bay.UpdatedAt = now;
        bay.UpdatedBy = currentUserId;

        await _writeDataService.SaveChangesInTransactionAsync(ct);
        await BayAdminSettingsHelper.WriteSettingsAsync(contentRootPath, settings);

        return BayAdminCommandResult.Ok(new { message = "Vehicle removed from bay successfully." });
    }

    public async Task<BayAdminCommandResult> MoveVehicleAsync(
        MoveVehicleRequest request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
    {
        if (request.VehicleId <= 0) return BayAdminCommandResult.BadRequest("VehicleId is required.");
        if (request.FromBayId <= 0 || request.ToBayId <= 0) return BayAdminCommandResult.BadRequest("FromBayId and ToBayId are required.");
        if (request.FromBayId == request.ToBayId) return BayAdminCommandResult.BadRequest("FromBayId and ToBayId must be different.");

        var bays = await _writeDataService.GetBaysByIdsAsync(new[] { request.FromBayId, request.ToBayId }, ct);

        var fromBay = bays.FirstOrDefault(b => b.Id == request.FromBayId);
        var toBay = bays.FirstOrDefault(b => b.Id == request.ToBayId);
        if (fromBay == null || toBay == null) return BayAdminCommandResult.BadRequest("Khoang nguồn hoặc đích không tồn tại.");

        var vehicle = await _vehicleLookupDataService.LoadVehicleDescriptorAsync(request.VehicleId, ct);
        if (vehicle == null) return BayAdminCommandResult.BadRequest("Không tìm thấy xe cần điều phối.");

        var settings = await BayAdminSettingsHelper.ReadSettingsAsync(contentRootPath);
        var metadata = BayAdminSettingsHelper.ResolveMetadata(settings, toBay.Id);
        var now = VietnamTime.Now;

        var sourceSession = settings.Sessions
            .Where(s => s.BayId == fromBay.Id && s.VehicleId == request.VehicleId && s.ReleasedAt == null)
            .OrderByDescending(s => s.AssignedAt)
            .FirstOrDefault();

        if (sourceSession == null && fromBay.CurrentOccupiedVehicleId != request.VehicleId)
        {
            return BayAdminCommandResult.BadRequest("Xe hiện không nằm trong khoang nguồn.");
        }

        var targetStatus = BayAdminSettingsHelper.NormalizeOperationalStatus(toBay.OperationalStatus);
        var invalidStateMessage = BayAdminSettingsHelper.ValidateBayForAssign(toBay, targetStatus);
        if (!string.IsNullOrWhiteSpace(invalidStateMessage))
        {
            return BayAdminCommandResult.BadRequest(invalidStateMessage);
        }

        var targetActiveVehicleSet = settings.Sessions
            .Where(s => s.BayId == toBay.Id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .ToHashSet();
        if (toBay.CurrentOccupiedVehicleId.HasValue)
        {
            targetActiveVehicleSet.Add(toBay.CurrentOccupiedVehicleId.Value);
        }

        if (targetActiveVehicleSet.Count >= metadata.Capacity)
        {
            return BayAdminCommandResult.BadRequest("Khoang đích đã đầy.");
        }

        if (!BayAdminSettingsHelper.IsSegmentCompatible(BayAdminSettingsHelper.NormalizeSegment(vehicle.Segment), metadata.SupportedSegments))
        {
            return BayAdminCommandResult.BadRequest("Khoang đích không phù hợp loại xe.");
        }

        var activeElsewhere = await BayAdminRowBuilder.FindVehicleActiveBayAsync(_vehicleLookupDataService, request.VehicleId, settings, ignoreBayId: fromBay.Id, ct: ct);
        if (activeElsewhere.HasValue && activeElsewhere.Value != toBay.Id)
        {
            return BayAdminCommandResult.BadRequest($"Xe đang bị gán ở khoang #{activeElsewhere.Value}.");
        }

        if (sourceSession != null)
        {
            sourceSession.ReleasedAt = now;
            sourceSession.Note = BayAdminSettingsHelper.MergeNote(sourceSession.Note, request.Note);
        }
        else
        {
            settings.Sessions.Add(new BaySession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                BayId = fromBay.Id,
                VehicleId = request.VehicleId,
                AssignedAt = fromBay.UpdatedAt ?? now,
                ReleasedAt = now,
                ServiceStatus = request.ServiceStatus?.Trim(),
                Note = $"Auto close from currentOccupiedVehicleId. {request.Note}".Trim()
            });
        }

        settings.Sessions.Add(new BaySession
        {
            SessionId = Guid.NewGuid().ToString("N"),
            BayId = toBay.Id,
            VehicleId = request.VehicleId,
            AssignedAt = now,
            ReleasedAt = null,
            ServiceStatus = request.ServiceStatus?.Trim(),
            Note = request.Note?.Trim()
        });

        var remainingInSource = settings.Sessions
            .Where(s => s.BayId == fromBay.Id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .Distinct()
            .ToList();

        fromBay.CurrentOccupiedVehicleId = remainingInSource.FirstOrDefault();
        if (fromBay.CurrentOccupiedVehicleId == 0) fromBay.CurrentOccupiedVehicleId = null;
        if (fromBay.CurrentOccupiedVehicleId == null && BayAdminSettingsHelper.NormalizeOperationalStatus(fromBay.OperationalStatus) == BayAdminConstants.StatusActive)
        {
            fromBay.OperationalStatus = BayAdminConstants.StatusActive;
        }
        fromBay.ReservationStatus = fromBay.CurrentOccupiedVehicleId.HasValue
            ? BayAdminConstants.ReservationStatusOccupied
            : BayAdminConstants.ReservationStatusAvailable;

        if (!toBay.CurrentOccupiedVehicleId.HasValue)
        {
            toBay.CurrentOccupiedVehicleId = request.VehicleId;
        }
        if (targetStatus == BayAdminConstants.StatusActive)
        {
            toBay.OperationalStatus = BayAdminConstants.StatusActive;
        }
        toBay.ReservationStatus = BayAdminConstants.ReservationStatusOccupied;

        fromBay.LastStatusChangedAt = now;
        fromBay.UpdatedAt = now;
        fromBay.UpdatedBy = currentUserId;
        toBay.LastStatusChangedAt = now;
        toBay.UpdatedAt = now;
        toBay.UpdatedBy = currentUserId;

        await _writeDataService.SaveChangesInTransactionAsync(ct);
        await BayAdminSettingsHelper.WriteSettingsAsync(contentRootPath, settings);

        return BayAdminCommandResult.Ok(new { message = "Vehicle moved successfully." });
    }
}
