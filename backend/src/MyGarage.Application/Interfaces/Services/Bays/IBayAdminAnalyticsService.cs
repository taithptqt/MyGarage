namespace MyGarage.Application.Bays;

public interface IBayAdminAnalyticsService
{
    Task<BayAdminCommandResult> GetUsageAsync(
        long? branchId,
        string contentRootPath,
        CancellationToken ct = default);

    Task<BayAdminCommandResult> GetLayoutAsync(
        long? branchId,
        string contentRootPath,
        CancellationToken ct = default);

    Task<BayAdminCommandResult> GetAlertsAsync(
        long? branchId,
        string contentRootPath,
        CancellationToken ct = default);

    Task<BayAdminCommandResult> GetSuggestionsAsync(
        long? vehicleId,
        long? branchId,
        string? preferredArea,
        long? sourceBayId,
        string? serviceStatus,
        string contentRootPath,
        CancellationToken ct = default);
}
