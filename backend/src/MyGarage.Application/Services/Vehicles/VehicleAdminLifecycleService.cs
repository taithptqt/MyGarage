using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Interfaces.Stores.Common;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class VehicleAdminLifecycleService : IVehicleAdminLifecycleService
{
    private readonly IVehicleAdminDataService _dataService;
    private readonly IAppConfigFileStore _configStore;

    public VehicleAdminLifecycleService(IVehicleAdminDataService dataService, IAppConfigFileStore configStore)
    {
        _dataService = dataService;
        _configStore = configStore;
    }

    public async Task<VehicleAdminCommandResult> CreateVehicleAsync(VehicleUpsertRequest? request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        if (request == null) return VehicleAdminCommandResult.BadRequest("Payload is required.");
        if (request.ModelId <= 0) return VehicleAdminCommandResult.BadRequest("ModelId is required.");
        if (!request.ManufactureYear.HasValue || request.ManufactureYear.Value <= 0)
        {
            return VehicleAdminCommandResult.BadRequest("Vui lòng nhập nĒm sản xuất.");
        }

        var modelExists = await _dataService.VehicleModelExistsAsync(request.ModelId, ct);
        if (!modelExists) return VehicleAdminCommandResult.BadRequest("Vehicle model does not exist.");

        var normalizedPlate = string.IsNullOrWhiteSpace(request.LicensePlate)
            ? $"TEMP-{Guid.NewGuid():N}".ToUpperInvariant()[..12]
            : request.LicensePlate.Trim().ToUpperInvariant();

        var duplicatedPlate = await _dataService.VehiclePlateExistsAsync(normalizedPlate, ct: ct);
        if (duplicatedPlate) return VehicleAdminCommandResult.BadRequest("License plate already exists.");

        if (!string.IsNullOrWhiteSpace(request.Vin))
        {
            var duplicatedVin = await _dataService.VehicleVinExistsAsync(request.Vin.Trim().ToUpperInvariant(), ct: ct);
            if (duplicatedVin) return VehicleAdminCommandResult.BadRequest("VIN already exists.");
        }

        var now = VietnamTime.Now;
        var userId = actor.UserId;
        var actorName = actor.UserDisplayName;
        var vehicle = new Vehicle
        {
            LicensePlate = normalizedPlate,
            Vin = string.IsNullOrWhiteSpace(request.Vin) ? null : request.Vin.Trim().ToUpperInvariant(),
            ModelId = request.ModelId,
            Color = VehicleAdminSupport.NormalizeNullableText(request.Color),
            Odo = request.Odo,
            ManufactureYear = request.ManufactureYear,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        await _dataService.AddVehicleAsync(vehicle, ct);

        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, vehicle.Id);
        VehicleAdminSupport.ApplyVehicleMetadata(metadata, request, now);
        if (request.BranchId.HasValue && request.BranchId.Value > 0) metadata.AssignedBranchId = request.BranchId.Value;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            VehicleAdminSupport.UpdateVehicleStatus(metadata, settings, vehicle.Id, VehicleAdminSupport.NormalizeVehicleStatus(request.Status), request.Note, userId, now, actorName);
        }
        if (request.CustomerId.HasValue)
        {
            var customerExists = await _dataService.CustomerExistsAsync(request.CustomerId.Value, ct);
            if (!customerExists) return VehicleAdminCommandResult.BadRequest("Customer does not exist.");
            await VehicleAdminSupport.SetCurrentOwnerAsync(_dataService, vehicle.Id, request.CustomerId, request.Note, now, userId, ct);
        }

        VehicleAdminSupport.AppendVehicleAudit(settings, vehicle.Id, "CREATE_VEHICLE", "Create vehicle by admin", userId, actorName, now);
        await _dataService.SaveChangesInTransactionAsync(ct);
        await VehicleAdminSupport.WriteSettingsAsync(_configStore, settings, ct);

        return VehicleAdminCommandResult.Ok(new
        {
            id = vehicle.Id,
            vehicleCode = VehicleAdminSupport.FormatVehicleCode(vehicle.Id),
            message = "Vehicle created successfully."
        });
    }

    public async Task<VehicleAdminCommandResult> UpdateVehicleAsync(long id, VehicleUpsertRequest? request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        if (request == null) return VehicleAdminCommandResult.BadRequest("Payload is required.");
        var vehicle = await _dataService.GetVehicleByIdAsync(id, ct);
        if (vehicle == null) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, id);
        if (metadata.IsEditLocked) return VehicleAdminCommandResult.BadRequest("Vehicle is locked for editing.");

        if (request.ModelId > 0 && request.ModelId != vehicle.ModelId)
        {
            var modelExists = await _dataService.VehicleModelExistsAsync(request.ModelId, ct);
            if (!modelExists) return VehicleAdminCommandResult.BadRequest("Vehicle model does not exist.");
            vehicle.ModelId = request.ModelId;
        }

        if (!string.IsNullOrWhiteSpace(request.LicensePlate))
        {
            var normalizedPlate = request.LicensePlate.Trim().ToUpperInvariant();
            if (!string.Equals(vehicle.LicensePlate, normalizedPlate, StringComparison.OrdinalIgnoreCase))
            {
                var duplicatedPlate = await _dataService.VehiclePlateExistsAsync(normalizedPlate, id, ct);
                if (duplicatedPlate) return VehicleAdminCommandResult.BadRequest("License plate already exists.");
                vehicle.LicensePlate = normalizedPlate;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Vin))
        {
            var normalizedVin = request.Vin.Trim().ToUpperInvariant();
            if (!string.Equals(vehicle.Vin, normalizedVin, StringComparison.OrdinalIgnoreCase))
            {
                var duplicatedVin = await _dataService.VehicleVinExistsAsync(normalizedVin, id, ct);
                if (duplicatedVin) return VehicleAdminCommandResult.BadRequest("VIN already exists.");
                vehicle.Vin = normalizedVin;
            }
        }

        if (request.Color != null) vehicle.Color = VehicleAdminSupport.NormalizeNullableText(request.Color);
        if (request.Odo.HasValue)
        {
            vehicle.Odo = request.Odo;
            vehicle.OdoLastUpdatedAt = VietnamTime.Now;
        }
        if (request.ManufactureYear.HasValue) vehicle.ManufactureYear = request.ManufactureYear;

        var now = VietnamTime.Now;
        var userId = actor.UserId;
        var actorName = actor.UserDisplayName;
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = userId;
        VehicleAdminSupport.ApplyVehicleMetadata(metadata, request, now);

        if (request.BranchId.HasValue && request.BranchId.Value > 0) metadata.AssignedBranchId = request.BranchId.Value;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            VehicleAdminSupport.UpdateVehicleStatus(metadata, settings, id, VehicleAdminSupport.NormalizeVehicleStatus(request.Status), request.Note, userId, now, actorName);
        }
        if (request.CustomerId.HasValue)
        {
            var customerExists = await _dataService.CustomerExistsAsync(request.CustomerId.Value, ct);
            if (!customerExists) return VehicleAdminCommandResult.BadRequest("Customer does not exist.");
            await VehicleAdminSupport.SetCurrentOwnerAsync(_dataService, id, request.CustomerId, request.Note, now, userId, ct);
        }

        VehicleAdminSupport.AppendVehicleAudit(settings, id, "UPDATE_VEHICLE", "Update vehicle info", userId, actorName, now);
        await _dataService.SaveChangesInTransactionAsync(ct);
        await VehicleAdminSupport.WriteSettingsAsync(_configStore, settings, ct);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle updated successfully." });
    }

    public Task<VehicleAdminCommandResult> LockEditAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default)
        => SetEditLockAsync(id, isLocked: true, actor, ct);

    public Task<VehicleAdminCommandResult> UnlockEditAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default)
        => SetEditLockAsync(id, isLocked: false, actor, ct);

    private async Task<VehicleAdminCommandResult> SetEditLockAsync(long id, bool isLocked, VehicleAdminActorContext actor, CancellationToken ct)
    {
        var exists = await _dataService.VehicleExistsAsync(id, ct: ct);
        if (!exists) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, id);
        var now = VietnamTime.Now;
        metadata.IsEditLocked = isLocked;
        metadata.UpdatedAt = now;

        VehicleAdminSupport.AppendVehicleAudit(
            settings,
            id,
            isLocked ? "LOCK_EDIT" : "UNLOCK_EDIT",
            isLocked ? "Lock edit vehicle" : "Unlock edit vehicle",
            actor.UserId,
            actor.UserDisplayName,
            now);
        await VehicleAdminSupport.WriteSettingsAsync(_configStore, settings, ct);

        return VehicleAdminCommandResult.Ok(new { message = isLocked ? "Vehicle edit locked." : "Vehicle edit unlocked." });
    }

    public async Task<VehicleAdminCommandResult> HideVehicleAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        var vehicle = await _dataService.GetVehicleByIdAsync(id, ct);
        if (vehicle == null) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var now = VietnamTime.Now;
        var userId = actor.UserId;
        vehicle.IsDeleted = true;
        vehicle.DeletedAt = now;
        vehicle.DeletedBy = userId;
        vehicle.UpdatedAt = now;
        vehicle.UpdatedBy = userId;

        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, id);
        metadata.IsHidden = true;
        metadata.UpdatedAt = now;
        VehicleAdminSupport.AppendVehicleAudit(settings, id, "HIDE_VEHICLE", "Hide vehicle", userId, actor.UserDisplayName, now);

        await _dataService.SaveChangesInTransactionAsync(ct);
        await VehicleAdminSupport.WriteSettingsAsync(_configStore, settings, ct);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle hidden successfully." });
    }

    public async Task<VehicleAdminCommandResult> UpdateStatusAsync(long id, ChangeStatusRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        var exists = await _dataService.VehicleExistsAsync(id, activeOnly: true, ct: ct);
        if (!exists) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        var normalizedStatus = VehicleAdminSupport.NormalizeVehicleStatus(request.Status);
        if (!VehicleAdminSupport.IsKnownVehicleStatus(normalizedStatus)) return VehicleAdminCommandResult.BadRequest("Invalid vehicle status.");

        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        var metadata = VehicleAdminSupport.ResolveVehicleMetadata(settings, id);
        var now = VietnamTime.Now;
        var userId = actor.UserId;
        var actorName = actor.UserDisplayName;

        VehicleAdminSupport.UpdateVehicleStatus(metadata, settings, id, normalizedStatus, request.Note, userId, now, actorName);
        VehicleAdminSupport.AppendVehicleAudit(settings, id, "SET_STATUS", $"Set status to {normalizedStatus}", userId, actorName, now);
        await VehicleAdminSupport.WriteSettingsAsync(_configStore, settings, ct);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle status updated." });
    }

    public async Task<VehicleAdminCommandResult> ChangeOwnerAsync(long id, ChangeOwnerRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
    {
        var exists = await _dataService.VehicleExistsAsync(id, activeOnly: true, ct: ct);
        if (!exists) return VehicleAdminCommandResult.NotFound("Vehicle not found.");

        if (request.CustomerId.HasValue)
        {
            var customerExists = await _dataService.CustomerExistsAsync(request.CustomerId.Value, ct);
            if (!customerExists) return VehicleAdminCommandResult.BadRequest("Customer does not exist.");
        }

        var now = VietnamTime.Now;
        var userId = actor.UserId;
        await VehicleAdminSupport.SetCurrentOwnerAsync(_dataService, id, request.CustomerId, request.Note, now, userId, ct);

        var settings = await VehicleAdminSupport.ReadSettingsAsync(_configStore, ct);
        VehicleAdminSupport.AppendVehicleAudit(
            settings,
            id,
            "CHANGE_OWNER",
            request.CustomerId.HasValue ? $"Change owner to customer #{request.CustomerId.Value}" : "Clear owner",
            userId,
            actor.UserDisplayName,
            now);

        await _dataService.SaveChangesInTransactionAsync(ct);
        await VehicleAdminSupport.WriteSettingsAsync(_configStore, settings, ct);

        return VehicleAdminCommandResult.Ok(new { message = "Vehicle owner updated." });
    }
}
