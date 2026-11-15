using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class VehicleAdminAssignmentService : IVehicleAdminAssignmentService
{
    private readonly IVehicleAdminDataService _dataService;

    public VehicleAdminAssignmentService(IVehicleAdminDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<VehicleAdminCommandResult> AssignStaffAsync(long id, AssignStaffRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        var exists = await _dataService.VehicleExistsAsync(id, activeOnly: true, ct: ct);
        if (!exists) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        Staff? staff = null;
        if (request.StaffId.HasValue)
        {
            staff = await _dataService.GetActiveStaffByIdAsync(request.StaffId.Value, ct);
            if (staff == null) return VehicleAdminCommandResult.BadRequest("Staff does not exist.");
        }

        var settings = await VehicleAdminSupport.ReadSettingsAsync(actor.ContentRootPath);
        var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, id);

        string? oldStaffName = null;
        if (metadata.CurrentStaffId.HasValue)
        {
            oldStaffName = await _dataService.GetStaffFullNameAsync(metadata.CurrentStaffId.Value, ct);
        }

        var now = VietnamTime.Now;
        var userId = actor.UserId;
        var actorName = actor.UserDisplayName;

        settings.AssignmentHistory.Add(new VehicleAssignmentHistoryEntry
        {
            VehicleId = id,
            OldStaffId = metadata.CurrentStaffId,
            OldStaffName = oldStaffName,
            NewStaffId = staff?.Id,
            NewStaffName = staff?.FullName,
            TechnicalTeam = string.IsNullOrWhiteSpace(request.TechnicalTeam) ? metadata.TechnicalTeam : request.TechnicalTeam.Trim(),
            Note = request.Note?.Trim(),
            AssignedAt = now,
            AssignedBy = userId,
            AssignedByName = actorName
        });

        metadata.CurrentStaffId = staff?.Id;
        if (!string.IsNullOrWhiteSpace(request.TechnicalTeam)) metadata.TechnicalTeam = request.TechnicalTeam.Trim();
        metadata.UpdatedAt = now;

        VehicleAdminSupport.AppendVehicleAudit(settings, id, "ASSIGN_STAFF", staff == null ? "Clear staff assignment" : $"Assign staff #{staff.Id}", userId, actorName, now);
        await VehicleAdminSupport.WriteSettingsAsync(actor.ContentRootPath, settings);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle assignment updated." });
    }

    public async Task<VehicleAdminCommandResult> AssignBayAsync(long id, AssignBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        if (request.BayId <= 0) return VehicleAdminCommandResult.BadRequest("BayId is required.");

        var vehicle = await _dataService.GetActiveVehicleWithModelAsync(id, ct);
        if (vehicle == null) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var bay = await _dataService.GetActiveBayByIdAsync(request.BayId, ct);
        if (bay == null) return VehicleAdminCommandResult.BadRequest("Bay does not exist.");

        var baySettings = await VehicleAdminSupport.ReadBaySettingsAsync(actor.ContentRootPath);
        var assignResult = await AssignVehicleToBayAsync(vehicle, bay, baySettings, request.ServiceStatus, request.Note, actor.UserId, ct);
        if (!assignResult.Success) return VehicleAdminCommandResult.BadRequest(assignResult.Message);

        var now = VietnamTime.Now;
        var userId = actor.UserId;
        var actorName = actor.UserDisplayName;
        var settings = await VehicleAdminSupport.ReadSettingsAsync(actor.ContentRootPath);

        if (!string.IsNullOrWhiteSpace(request.NewVehicleStatus))
        {
            var normalizedStatus = VehicleAdminSupport.NormalizeVehicleStatus(request.NewVehicleStatus);
            if (VehicleAdminSupport.IsKnownVehicleStatus(normalizedStatus))
            {
                var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, id);
                VehicleAdminSupport.UpdateVehicleStatus(metadata, settings, id, normalizedStatus, request.Note, userId, now, actorName);
                VehicleAdminSupport.AppendVehicleAudit(settings, id, "SET_STATUS", $"Set status to {normalizedStatus} on bay assignment", userId, actorName, now);
            }
        }

        VehicleAdminSupport.AppendVehicleAudit(settings, id, "ASSIGN_BAY", $"Assign to bay #{request.BayId}", userId, actorName, now);

        await _dataService.SaveChangesInTransactionAsync(ct);
        await VehicleAdminSupport.WriteBaySettingsAsync(actor.ContentRootPath, baySettings);
        await VehicleAdminSupport.WriteSettingsAsync(actor.ContentRootPath, settings);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle assigned to bay." });
    }

    public async Task<VehicleAdminCommandResult> MoveBayAsync(long id, MoveBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        if (request.ToBayId <= 0) return VehicleAdminCommandResult.BadRequest("ToBayId is required.");

        var vehicle = await _dataService.GetActiveVehicleWithModelAsync(id, ct);
        if (vehicle == null) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var baySettings = await VehicleAdminSupport.ReadBaySettingsAsync(actor.ContentRootPath);
        var targetBay = await _dataService.GetActiveBayByIdAsync(request.ToBayId, ct);
        if (targetBay == null) return VehicleAdminCommandResult.BadRequest("Target bay does not exist.");

        var now = VietnamTime.Now;
        var userId = actor.UserId;
        await ReleaseActiveBaySessionsAsync(id, baySettings, request.Note, mergeEmptyNote: true, now, userId, ct);

        var fallbackBays = await _dataService.GetActiveBaysOccupiedByVehicleAsync(id, ct);
        foreach (var fb in fallbackBays)
        {
            ReleaseBayOccupancy(fb, now, userId);
        }

        var assignResult = await AssignVehicleToBayAsync(vehicle, targetBay, baySettings, request.ServiceStatus, request.Note, userId, ct);
        if (!assignResult.Success) return VehicleAdminCommandResult.BadRequest(assignResult.Message);

        var actorName = actor.UserDisplayName;
        var settings = await VehicleAdminSupport.ReadSettingsAsync(actor.ContentRootPath);

        if (!string.IsNullOrWhiteSpace(request.NewVehicleStatus))
        {
            var normalizedStatus = VehicleAdminSupport.NormalizeVehicleStatus(request.NewVehicleStatus);
            if (VehicleAdminSupport.IsKnownVehicleStatus(normalizedStatus))
            {
                var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, id);
                VehicleAdminSupport.UpdateVehicleStatus(metadata, settings, id, normalizedStatus, request.Note, userId, now, actorName);
                VehicleAdminSupport.AppendVehicleAudit(settings, id, "SET_STATUS", $"Set status to {normalizedStatus} on move bay", userId, actorName, now);
            }
        }

        VehicleAdminSupport.AppendVehicleAudit(settings, id, "MOVE_BAY", $"Move vehicle to bay #{request.ToBayId}", userId, actorName, now);

        await _dataService.SaveChangesInTransactionAsync(ct);
        await VehicleAdminSupport.WriteBaySettingsAsync(actor.ContentRootPath, baySettings);
        await VehicleAdminSupport.WriteSettingsAsync(actor.ContentRootPath, settings);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle moved to new bay." });
    }

    public async Task<VehicleAdminCommandResult> RemoveBayAsync(long id, RemoveBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        var exists = await _dataService.VehicleExistsAsync(id, activeOnly: true, ct: ct);
        if (!exists) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var baySettings = await VehicleAdminSupport.ReadBaySettingsAsync(actor.ContentRootPath);
        var now = VietnamTime.Now;
        var userId = actor.UserId;
        await ReleaseActiveBaySessionsAsync(id, baySettings, request.Note, mergeEmptyNote: false, now, userId, ct);

        var fallbackBay = await _dataService.GetFirstActiveBayOccupiedByVehicleAsync(id, ct);
        if (fallbackBay != null)
        {
            ReleaseBayOccupancy(fallbackBay, now, userId);
        }

        var settings = await VehicleAdminSupport.ReadSettingsAsync(actor.ContentRootPath);
        VehicleAdminSupport.AppendVehicleAudit(settings, id, "REMOVE_BAY", "Remove vehicle from bay", userId, actor.UserDisplayName, now);

        await _dataService.SaveChangesInTransactionAsync(ct);
        await VehicleAdminSupport.WriteBaySettingsAsync(actor.ContentRootPath, baySettings);
        await VehicleAdminSupport.WriteSettingsAsync(actor.ContentRootPath, settings);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle removed from bay." });
    }

    private async Task ReleaseActiveBaySessionsAsync(
        long vehicleId,
        BaySettingsSnapshot baySettings,
        string? note,
        bool mergeEmptyNote,
        DateTime now,
        long? userId,
        CancellationToken ct)
    {
        var activeSessions = baySettings.Sessions.Where(s => s.VehicleId == vehicleId && s.ReleasedAt == null).ToList();
        foreach (var session in activeSessions)
        {
            session.ReleasedAt = now;
            if (mergeEmptyNote || !string.IsNullOrWhiteSpace(note))
            {
                session.Note = VehicleAdminSupport.MergeNote(session.Note, note);
            }

            var bay = await _dataService.GetBayByIdAsync(session.BayId, ct);
            if (bay == null) continue;
            if (!mergeEmptyNote && bay.CurrentOccupiedVehicleId != vehicleId) continue;

            var remaining = baySettings.Sessions
                .Where(s => s.BayId == bay.Id && s.ReleasedAt == null && s.VehicleId != vehicleId)
                .Select(s => s.VehicleId)
                .FirstOrDefault();

            bay.CurrentOccupiedVehicleId = remaining > 0 ? remaining : null;
            if (!bay.CurrentOccupiedVehicleId.HasValue && VehicleAdminSupport.NormalizeBayStatus(bay.OperationalStatus) == VehicleAdminSupport.BayStatusInUse)
            {
                bay.OperationalStatus = VehicleAdminSupport.BayStatusEmpty;
            }
            bay.UpdatedAt = now;
            bay.UpdatedBy = userId;
        }
    }

    private static void ReleaseBayOccupancy(Bay bay, DateTime now, long? userId)
    {
        bay.CurrentOccupiedVehicleId = null;
        if (VehicleAdminSupport.NormalizeBayStatus(bay.OperationalStatus) == VehicleAdminSupport.BayStatusInUse)
        {
            bay.OperationalStatus = VehicleAdminSupport.BayStatusEmpty;
        }
        bay.UpdatedAt = now;
        bay.UpdatedBy = userId;
    }

    private async Task<AssignBayResult> AssignVehicleToBayAsync(
        Vehicle vehicle,
        Bay bay,
        BaySettingsSnapshot baySettings,
        string? serviceStatus,
        string? note,
        long? userId,
        CancellationToken ct)
    {
        var status = VehicleAdminSupport.NormalizeBayStatus(bay.OperationalStatus);
        if (bay.IsDeleted == true) return AssignBayResult.Fail("Bay is hidden.");
        if (bay.IsActive == false) return AssignBayResult.Fail("Bay is locked.");
        if (status == VehicleAdminSupport.BayStatusMaintenance) return AssignBayResult.Fail("Bay is under maintenance.");
        if (status == VehicleAdminSupport.BayStatusSuspended) return AssignBayResult.Fail("Bay is suspended.");

        var metadata = VehicleAdminSupport.ResolveBayMetadata(baySettings, bay.Id);
        var activeVehicleSet = baySettings.Sessions
            .Where(s => s.BayId == bay.Id && s.ReleasedAt == null)
            .Select(s => s.VehicleId)
            .ToHashSet();
        if (bay.CurrentOccupiedVehicleId.HasValue) activeVehicleSet.Add(bay.CurrentOccupiedVehicleId.Value);
        if (activeVehicleSet.Count >= metadata.Capacity) return AssignBayResult.Fail("Bay is full.");

        var activeAnywhere = baySettings.Sessions
            .Where(s => s.VehicleId == vehicle.Id && s.ReleasedAt == null)
            .OrderByDescending(s => s.AssignedAt)
            .FirstOrDefault();
        if (activeAnywhere != null)
            return AssignBayResult.Fail(activeAnywhere.BayId == bay.Id ? "Vehicle is already assigned to this bay." : $"Vehicle is currently assigned in bay #{activeAnywhere.BayId}. Please move or remove it first.");

        var fallbackAnywhere = await _dataService.FindOccupiedBayIdByVehicleAsync(vehicle.Id, ct);
        if (fallbackAnywhere.HasValue)
            return AssignBayResult.Fail(fallbackAnywhere.Value == bay.Id ? "Vehicle is already assigned to this bay." : $"Vehicle is currently assigned in bay #{fallbackAnywhere.Value}. Please move or remove it first.");

        if (!VehicleAdminSupport.IsSegmentCompatible(VehicleAdminSupport.NormalizeSegment(vehicle.Model?.Segment), metadata.SupportedSegments))
        {
            return AssignBayResult.Fail("Bay is not compatible with this vehicle segment.");
        }

        var now = VietnamTime.Now;
        baySettings.Sessions.Add(new BaySessionSnapshot
        {
            SessionId = Guid.NewGuid().ToString("N"),
            BayId = bay.Id,
            VehicleId = vehicle.Id,
            AssignedAt = now,
            ReleasedAt = null,
            ServiceStatus = VehicleAdminSupport.NormalizeNullableText(serviceStatus),
            Note = VehicleAdminSupport.NormalizeNullableText(note)
        });

        if (!bay.CurrentOccupiedVehicleId.HasValue) bay.CurrentOccupiedVehicleId = vehicle.Id;
        if (status == VehicleAdminSupport.BayStatusEmpty) bay.OperationalStatus = VehicleAdminSupport.BayStatusInUse;
        bay.UpdatedAt = now;
        bay.UpdatedBy = userId;

        return AssignBayResult.Ok();
    }
}
