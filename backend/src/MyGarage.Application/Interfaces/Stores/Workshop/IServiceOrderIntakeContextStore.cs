using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderIntakeContextStore
{
    Task<bool> IsBranchDayOffAsync(long branchId, int dayOfWeek);

    Task<Customer?> FindActiveCustomerByNormalizedPhoneAsync(string normalizedPhone);
    Task<Vehicle?> FindVehicleByLicensePlateAsync(string licensePlate);
    Task<CustomerVehicle?> FindCustomerVehicleAsync(long customerId, long vehicleId);
    Task<bool> CustomerExistsAsync(long customerId);
    Task<bool> VehicleOwnedByCustomerAsync(long customerId, long vehicleId);
    Task<VehicleEngineFuelInfo?> GetVehicleEngineFuelInfoAsync(long vehicleId);

    Task<List<Package>> GetActivePackagesWithItemsAsync(IReadOnlyList<long> packageIds);
    Task<Dictionary<long, StandardService>> GetActiveStandardServicesMapAsync(IReadOnlyList<long> serviceIds);

    Task<long?> GetAppointmentInsuranceCompanyIdAsync(long appointmentId);
    Task<bool> ActiveInsuranceCompanyExistsAsync(long insuranceCompanyId);
    Task<List<ServiceOrderInsuranceAsset>> GetInsuranceAssetsByOrderIdAsync(long serviceOrderId);
    Task<List<ServiceOrderInsuranceUpload>> GetStagedInsuranceUploadsAsync(IReadOnlyList<string> normalizedUrls, string stagedStatus);
}
