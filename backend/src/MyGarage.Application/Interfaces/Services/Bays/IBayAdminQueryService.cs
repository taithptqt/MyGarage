namespace MyGarage.Application.Bays;

public interface IBayAdminQueryService
{
    Task<BayAdminCommandResult> GetLookupsAsync(int takeVehicles, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetListAsync(
        long? branchId,
        string? search,
        string? area,
        string? status,
        string? sortBy,
        string? sortDirection,
        bool includeHidden,
        CancellationToken ct = default);

    Task<BayAdminCommandResult> GetByIdAsync(long id, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetSummaryAsync(long? branchId, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetBayVehiclesAsync(long id, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetBayHistoryAsync(long id, int take, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetUsageAsync(long? branchId, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetLayoutAsync(long? branchId, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetAlertsAsync(long? branchId, CancellationToken ct = default);

    Task<BayAdminCommandResult> GetSuggestionsAsync(
        long? vehicleId,
        long? branchId,
        string? preferredArea,
        long? sourceBayId,
        string? serviceStatus,
        CancellationToken ct = default);
}
