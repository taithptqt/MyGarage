namespace MyGarage.Application.Bays;

public interface IBayAdminVehicleAssignmentService
{
    Task<BayAdminCommandResult> AssignVehicleAsync(long id, AssignVehicleRequest request, long? currentUserId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> RemoveVehicleFromBayAsync(long id, RemoveVehicleRequest request, long? currentUserId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> MoveVehicleAsync(MoveVehicleRequest request, long? currentUserId, string contentRootPath, CancellationToken ct = default);
}
