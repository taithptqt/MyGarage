namespace MyGarage.Interfaces;

public interface IVehicleAdminQueryService
{
    Task<object> GetLookupsAsync(CancellationToken ct = default);
    Task<object> GetListAsync(VehicleAdminListQuery query, CancellationToken ct = default);
    Task<object> GetSummaryAsync(long? branchId, CancellationToken ct = default);
    Task<object> GetAlertsAsync(long? branchId, CancellationToken ct = default);
    Task<VehicleAdminCommandResult> GetVehicleDetailAsync(long id, int takeHistory, CancellationToken ct = default);
    Task<object> GetHistoryAsync(long? vehicleId, long? branchId, int take, CancellationToken ct = default);
}
