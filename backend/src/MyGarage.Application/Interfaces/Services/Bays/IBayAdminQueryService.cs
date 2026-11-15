namespace MyGarage.Application.Bays;

public interface IBayAdminQueryService
{
    Task<BayAdminCommandResult> GetLookupsAsync(int takeVehicles, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetListAsync(
        long? branchId,
        string? search,
        string? area,
        string? status,
        string? sortBy,
        string? sortDirection,
        bool includeHidden,
        string contentRootPath,
        CancellationToken ct = default);

    Task<BayAdminCommandResult> GetByIdAsync(long id, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetSummaryAsync(long? branchId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetBayVehiclesAsync(long id, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetBayHistoryAsync(long id, int take, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetUsageAsync(long? branchId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetLayoutAsync(long? branchId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetAlertsAsync(long? branchId, string contentRootPath, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetSuggestionsAsync(
        long? vehicleId,
        long? branchId,
        string? preferredArea,
        long? sourceBayId,
        string? serviceStatus,
        string contentRootPath,
        CancellationToken ct = default);
}
