namespace MyGarage.Application.Bays;

public sealed class BayAdminCommandFacade : IBayAdminCommandFacade
{
    private readonly IBayAdminQueryService _queryService;
    private readonly IBayAdminManagementService _managementService;
    private readonly IBayAdminVehicleAssignmentService _assignmentService;

    public BayAdminCommandFacade(
        IBayAdminQueryService queryService,
        IBayAdminManagementService managementService,
        IBayAdminVehicleAssignmentService assignmentService)
    {
        _queryService = queryService;
        _managementService = managementService;
        _assignmentService = assignmentService;
    }

    public Task<BayAdminCommandResult> GetLookupsAsync(
        int takeVehicles,
        CancellationToken ct = default)
        => _queryService.GetLookupsAsync(takeVehicles, ct);

    public Task<BayAdminCommandResult> GetListAsync(
        long? branchId,
        string? search,
        string? area,
        string? status,
        string? sortBy,
        string? sortDirection,
        bool includeHidden,
        CancellationToken ct = default)
        => _queryService.GetListAsync(
            branchId,
            search,
            area,
            status,
            sortBy,
            sortDirection,
            includeHidden,
            ct);

    public Task<BayAdminCommandResult> GetByIdAsync(
        long id,
        CancellationToken ct = default)
        => _queryService.GetByIdAsync(id, ct);

    public Task<BayAdminCommandResult> GetSummaryAsync(
        long? branchId,
        CancellationToken ct = default)
        => _queryService.GetSummaryAsync(branchId, ct);

    public Task<BayAdminCommandResult> GetBayVehiclesAsync(
        long id,
        CancellationToken ct = default)
        => _queryService.GetBayVehiclesAsync(id, ct);

    public Task<BayAdminCommandResult> GetBayHistoryAsync(
        long id,
        int take,
        CancellationToken ct = default)
        => _queryService.GetBayHistoryAsync(id, take, ct);

    public Task<BayAdminCommandResult> CreateBayAsync(
        BayUpsertRequest? request,
        long? currentUserId,
        CancellationToken ct = default)
        => _managementService.CreateBayAsync(request, currentUserId, ct);

    public Task<BayAdminCommandResult> UpdateBayAsync(
        long id,
        BayUpsertRequest request,
        long? currentUserId,
        CancellationToken ct = default)
        => _managementService.UpdateBayAsync(id, request, currentUserId, ct);

    public Task<BayAdminCommandResult> HideBayAsync(
        long id,
        long? currentUserId,
        CancellationToken ct = default)
        => _managementService.HideBayAsync(id, currentUserId, ct);

    public Task<BayAdminCommandResult> LockBayAsync(
        long id,
        long? currentUserId,
        CancellationToken ct = default)
        => _managementService.LockBayAsync(id, currentUserId, ct);

    public Task<BayAdminCommandResult> UnlockBayAsync(
        long id,
        long? currentUserId,
        CancellationToken ct = default)
        => _managementService.UnlockBayAsync(id, currentUserId, ct);

    public Task<BayAdminCommandResult> UpdateBayStatusAsync(
        long id,
        UpdateBayStatusRequest request,
        long? currentUserId,
        CancellationToken ct = default)
        => _managementService.UpdateBayStatusAsync(id, request, currentUserId, ct);

    public Task<BayAdminCommandResult> AssignVehicleAsync(
        long id,
        AssignVehicleRequest request,
        long? currentUserId,
        CancellationToken ct = default)
        => _assignmentService.AssignVehicleAsync(id, request, currentUserId, ct);

    public Task<BayAdminCommandResult> RemoveVehicleFromBayAsync(
        long id,
        RemoveVehicleRequest request,
        long? currentUserId,
        CancellationToken ct = default)
        => _assignmentService.RemoveVehicleFromBayAsync(id, request, currentUserId, ct);

    public Task<BayAdminCommandResult> MoveVehicleAsync(
        MoveVehicleRequest request,
        long? currentUserId,
        CancellationToken ct = default)
        => _assignmentService.MoveVehicleAsync(request, currentUserId, ct);

    public Task<BayAdminCommandResult> GetUsageAsync(
        long? branchId,
        CancellationToken ct = default)
        => _queryService.GetUsageAsync(branchId, ct);

    public Task<BayAdminCommandResult> GetLayoutAsync(
        long? branchId,
        CancellationToken ct = default)
        => _queryService.GetLayoutAsync(branchId, ct);

    public Task<BayAdminCommandResult> GetAlertsAsync(
        long? branchId,
        CancellationToken ct = default)
        => _queryService.GetAlertsAsync(branchId, ct);

    public Task<BayAdminCommandResult> GetSuggestionsAsync(
        long? vehicleId,
        long? branchId,
        string? preferredArea,
        long? sourceBayId,
        string? serviceStatus,
        CancellationToken ct = default)
        => _queryService.GetSuggestionsAsync(
            vehicleId,
            branchId,
            preferredArea,
            sourceBayId,
            serviceStatus,
            ct);
}
