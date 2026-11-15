namespace MyGarage.Interfaces;

public interface IVehicleAdminQueryService
{
    Task<object> GetLookupsAsync(string contentRootPath, CancellationToken ct = default);
    Task<object> GetListAsync(VehicleAdminListQuery query, string contentRootPath, CancellationToken ct = default);
    Task<object> GetSummaryAsync(long? branchId, string contentRootPath, CancellationToken ct = default);
    Task<object> GetAlertsAsync(long? branchId, string contentRootPath, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> GetVehicleDetailAsync(long id, int takeHistory, string contentRootPath, CancellationToken ct = default);
    Task<object> GetHistoryAsync(long? vehicleId, long? branchId, int take, string contentRootPath, CancellationToken ct = default);
}
