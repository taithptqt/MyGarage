using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Vehicles.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Vehicles;

public interface IVehicleStore
{
    Task<(IReadOnlyList<VehicleAdminItemDto> Items, int TotalCount)> GetVehicleAdminPageAsync(
        VehicleAdminPageQuery query,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Vehicle?> GetVehicleDetailByIdAsync(long id, CancellationToken ct = default);

    Task<Vehicle?> GetVehicleForUpdateAsync(long id, CancellationToken ct = default);

    Task<Vehicle?> GetVehicleForOwnerChangeAsync(long id, CancellationToken ct = default);

    Task<Vehicle?> GetVehicleForHideAsync(long id, CancellationToken ct = default);

    Task<bool> PlateExistsAsync(string plate, long? excludeVehicleId = null, CancellationToken ct = default);

    Task<bool> VinExistsAsync(string vin, long? excludeVehicleId = null, CancellationToken ct = default);

    Task<VehicleModel?> GetActiveModelWithBrandAsync(long modelId, CancellationToken ct = default);

    Task<bool> ActiveModelExistsAsync(long modelId, CancellationToken ct = default);

    Task<bool> ActiveCustomerExistsAsync(long customerId, CancellationToken ct = default);

    Task CreateVehicleAsync(Vehicle vehicle, CustomerVehicle? ownerLink, CancellationToken ct = default);

    Task SaveVehicleWithNewOwnerLinksAsync(Vehicle vehicle, IReadOnlyList<CustomerVehicle> newLinks, CancellationToken ct = default);
}
