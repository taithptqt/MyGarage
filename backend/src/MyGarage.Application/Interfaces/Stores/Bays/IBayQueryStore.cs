using MyGarage.Models;

namespace MyGarage.Application.Bays;

public interface IBayQueryStore
{
    Task<IReadOnlyList<Bay>> GetActiveBaysAsync(long? branchId, CancellationToken ct = default);
    Task<Dictionary<long, string>> GetVehicleLicensePlatesAsync(IReadOnlyCollection<long> vehicleIds, CancellationToken ct = default);
    Task<Bay?> GetBayWithVehicleAsync(long id, CancellationToken ct = default);
    Task<Bay?> GetBayForUpdateAsync(long id, CancellationToken ct = default);
    Task<Bay?> FindBayByIdAsync(long id, CancellationToken ct = default);
    Task<ServiceOrder?> FindServiceOrderByIdAsync(long id, CancellationToken ct = default);
    Task<Vehicle?> FindVehicleByIdAsync(long id, CancellationToken ct = default);
}
