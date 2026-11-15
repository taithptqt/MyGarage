using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public interface IVehicleAdminEntityDataService
{
    Task SaveChangesInTransactionAsync(CancellationToken ct = default);

    Task<bool> VehicleModelExistsAsync(long modelId, CancellationToken ct = default);
    Task<bool> VehiclePlateExistsAsync(string normalizedPlate, long? excludeVehicleId = null, CancellationToken ct = default);
    Task<bool> VehicleVinExistsAsync(string normalizedVin, long? excludeVehicleId = null, CancellationToken ct = default);
    Task<bool> CustomerExistsAsync(long customerId, CancellationToken ct = default);
    Task<bool> VehicleExistsAsync(long id, bool activeOnly = false, CancellationToken ct = default);

    Task AddVehicleAsync(Vehicle vehicle, CancellationToken ct = default);
    Task<Vehicle?> GetVehicleByIdAsync(long id, CancellationToken ct = default);
    Task<Vehicle?> GetActiveVehicleWithModelAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerVehicle>> GetActiveCustomerVehicleLinksAsync(long vehicleId, CancellationToken ct = default);
    void AddCustomerVehicle(CustomerVehicle entity);
}
