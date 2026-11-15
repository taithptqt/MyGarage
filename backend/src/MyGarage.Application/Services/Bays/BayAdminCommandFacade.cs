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
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetLookupsAsync(takeVehicles, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetListAsync(
        long? branchId,
        string? search,
        string? area,
        string? status,
        string? sortBy,
        string? sortDirection,
        bool includeHidden,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetListAsync(
            branchId,
            search,
            area,
            status,
            sortBy,
            sortDirection,
            includeHidden,
            contentRootPath,
            ct);

    public Task<BayAdminCommandResult> GetByIdAsync(
        long id,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetByIdAsync(id, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetSummaryAsync(
        long? branchId,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetSummaryAsync(branchId, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetBayVehiclesAsync(
        long id,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetBayVehiclesAsync(id, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetBayHistoryAsync(
        long id,
        int take,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetBayHistoryAsync(id, take, contentRootPath, ct);

    public Task<BayAdminCommandResult> CreateBayAsync(
        BayUpsertRequest? request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
        => _managementService.CreateBayAsync(request, currentUserId, contentRootPath, ct);

    public Task<BayAdminCommandResult> UpdateBayAsync(
        long id,
        BayUpsertRequest request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
        => _managementService.UpdateBayAsync(id, request, currentUserId, contentRootPath, ct);

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
        string contentRootPath,
        CancellationToken ct = default)
        => _managementService.UpdateBayStatusAsync(id, request, currentUserId, contentRootPath, ct);

    public Task<BayAdminCommandResult> AssignVehicleAsync(
        long id,
        AssignVehicleRequest request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
        => _assignmentService.AssignVehicleAsync(id, request, currentUserId, contentRootPath, ct);

    public Task<BayAdminCommandResult> RemoveVehicleFromBayAsync(
        long id,
        RemoveVehicleRequest request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
        => _assignmentService.RemoveVehicleFromBayAsync(id, request, currentUserId, contentRootPath, ct);

    public Task<BayAdminCommandResult> MoveVehicleAsync(
        MoveVehicleRequest request,
        long? currentUserId,
        string contentRootPath,
        CancellationToken ct = default)
        => _assignmentService.MoveVehicleAsync(request, currentUserId, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetUsageAsync(
        long? branchId,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetUsageAsync(branchId, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetLayoutAsync(
        long? branchId,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetLayoutAsync(branchId, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetAlertsAsync(
        long? branchId,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetAlertsAsync(branchId, contentRootPath, ct);

    public Task<BayAdminCommandResult> GetSuggestionsAsync(
        long? vehicleId,
        long? branchId,
        string? preferredArea,
        long? sourceBayId,
        string? serviceStatus,
        string contentRootPath,
        CancellationToken ct = default)
        => _queryService.GetSuggestionsAsync(
            vehicleId,
            branchId,
            preferredArea,
            sourceBayId,
            serviceStatus,
            contentRootPath,
            ct);
}
