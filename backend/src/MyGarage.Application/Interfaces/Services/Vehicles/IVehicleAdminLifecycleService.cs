namespace MyGarage.Interfaces;

public interface IVehicleAdminLifecycleService
{
    Task<VehicleAdminCommandResult> CreateVehicleAsync(VehicleUpsertRequest? request, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> UpdateVehicleAsync(long id, VehicleUpsertRequest? request, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> LockEditAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> UnlockEditAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> HideVehicleAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> UpdateStatusAsync(long id, ChangeStatusRequest request, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> ChangeOwnerAsync(long id, ChangeOwnerRequest request, VehicleAdminActorContext actor, CancellationToken ct = default);
}
