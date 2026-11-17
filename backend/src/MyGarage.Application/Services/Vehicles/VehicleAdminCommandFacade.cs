using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class VehicleAdminCommandFacade : IVehicleAdminCommandFacade
{
    private readonly IVehicleAdminQueryService _queryService;
    private readonly IVehicleAdminLifecycleService _lifecycleService;
    private readonly IVehicleAdminAssignmentService _assignmentService;

    public VehicleAdminCommandFacade(
        IVehicleAdminQueryService queryService,
        IVehicleAdminLifecycleService lifecycleService,
        IVehicleAdminAssignmentService assignmentService)
    {
        _queryService = queryService;
        _lifecycleService = lifecycleService;
        _assignmentService = assignmentService;
    }

    public Task<object> GetLookupsAsync(CancellationToken ct = default)
        => _queryService.GetLookupsAsync(ct);

    public Task<object> GetListAsync(VehicleAdminListQuery query, CancellationToken ct = default)
        => _queryService.GetListAsync(query, ct);

    public Task<object> GetSummaryAsync(long? branchId, CancellationToken ct = default)
        => _queryService.GetSummaryAsync(branchId, ct);

    public Task<object> GetAlertsAsync(long? branchId, CancellationToken ct = default)
        => _queryService.GetAlertsAsync(branchId, ct);

    public Task<VehicleAdminCommandResult> GetVehicleDetailAsync(long id, int takeHistory, CancellationToken ct = default)
        => _queryService.GetVehicleDetailAsync(id, takeHistory, ct);

    public Task<object> GetHistoryAsync(long? vehicleId, long? branchId, int take, CancellationToken ct = default)
        => _queryService.GetHistoryAsync(vehicleId, branchId, take, ct);

    public Task<VehicleAdminCommandResult> CreateVehicleAsync(VehicleUpsertRequest? request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _lifecycleService.CreateVehicleAsync(request, actor, ct);

    public Task<VehicleAdminCommandResult> UpdateVehicleAsync(long id, VehicleUpsertRequest? request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _lifecycleService.UpdateVehicleAsync(id, request, actor, ct);

    public Task<VehicleAdminCommandResult> LockEditAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _lifecycleService.LockEditAsync(id, actor, ct);

    public Task<VehicleAdminCommandResult> UnlockEditAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _lifecycleService.UnlockEditAsync(id, actor, ct);

    public Task<VehicleAdminCommandResult> HideVehicleAsync(long id, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _lifecycleService.HideVehicleAsync(id, actor, ct);

    public Task<VehicleAdminCommandResult> UpdateStatusAsync(long id, ChangeStatusRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _lifecycleService.UpdateStatusAsync(id, request, actor, ct);

    public Task<VehicleAdminCommandResult> ChangeOwnerAsync(long id, ChangeOwnerRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _lifecycleService.ChangeOwnerAsync(id, request, actor, ct);

    public Task<VehicleAdminCommandResult> AssignStaffAsync(long id, AssignStaffRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _assignmentService.AssignStaffAsync(id, request, actor, ct);

    public Task<VehicleAdminCommandResult> AssignBayAsync(long id, AssignBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _assignmentService.AssignBayAsync(id, request, actor, ct);

    public Task<VehicleAdminCommandResult> MoveBayAsync(long id, MoveBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _assignmentService.MoveBayAsync(id, request, actor, ct);

    public Task<VehicleAdminCommandResult> RemoveBayAsync(long id, RemoveBayRequest request, VehicleAdminActorContext actor, CancellationToken ct = default)
        => _assignmentService.RemoveBayAsync(id, request, actor, ct);
}
