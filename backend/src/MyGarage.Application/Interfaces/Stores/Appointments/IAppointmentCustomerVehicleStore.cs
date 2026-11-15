using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentCustomerVehicleStore
{
    Task<Customer?> GetCustomerByIdAsync(long customerId, bool asNoTracking = false);
    Task<bool> ActiveCustomerExistsAsync(long customerId);
    Task<Customer?> FindActiveCustomerByNormalizedPhoneAsync(string normalizedPhone);
    Task<List<Customer>> GetCustomersByIdsAsync(IReadOnlyCollection<long> customerIds);
    Task<Vehicle?> GetVehicleByIdNoTrackingAsync(long vehicleId);
    Task<Vehicle?> GetVehicleByPlateAsync(string plate);
    Task<long?> GetVehicleModelIdAsync(long vehicleId, bool requireNotDeleted = false);
    Task<Dictionary<long, long>> GetActiveVehicleModelIdsByVehicleIdsAsync(IReadOnlyCollection<long> vehicleIds);
    Task<string?> GetVehicleModelFuelTypeAsync(long vehicleModelId);
    Task<bool> CustomerOwnsVehicleAsync(long customerId, long vehicleId);
    Task<CustomerVehicle?> GetCustomerVehicleLinkAsync(long customerId, long vehicleId);
}
