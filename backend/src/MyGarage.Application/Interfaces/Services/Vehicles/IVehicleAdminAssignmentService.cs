namespace MyGarage.Interfaces;

public interface IVehicleAdminAssignmentService
{
    Task<VehicleAdminCommandResult> AssignStaffAsync(long id, AssignStaffRequest request, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> AssignBayAsync(long id, AssignBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> MoveBayAsync(long id, MoveBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> RemoveBayAsync(long id, RemoveBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default);
}
