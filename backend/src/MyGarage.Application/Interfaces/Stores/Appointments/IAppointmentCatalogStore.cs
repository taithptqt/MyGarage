using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentCatalogStore
{
    Task<List<StandardServiceLookupItem>> GetActiveStandardServiceLookupsAsync();
    Task<int?> GetStandardServiceMinutesAsync(long serviceId);
    Task<List<StandardService>> GetActiveStandardServicesByIdsAsync(IReadOnlyCollection<long> ids, bool includeCategory = false);
    Task<List<StandardServiceLookupItem>> GetActiveStandardServiceLookupsByIdsAsync(IReadOnlyCollection<long> ids);
    Task<Dictionary<long, StandardService>> GetActiveStandardServiceMapAsync(IReadOnlyCollection<long> ids);
    Task<Dictionary<long, string>> GetStandardServiceNamesByIdsAsync(IReadOnlyCollection<long> ids);
    Task<int> CountActivePackagesByIdsAsync(IReadOnlyCollection<long> packageIds);
    Task<List<Package>> GetActivePackagesByIdsAsync(IReadOnlyCollection<long> packageIds, bool includeCategoryAndItems = true);
    Task<List<long>> GetPackageItemServiceIdsAsync(IReadOnlyCollection<long> packageIds);
    Task<Dictionary<long, List<long>>> GetPackageItemServiceIdsByPackageIdsAsync(IReadOnlyCollection<long> packageIds);
    Task<Dictionary<long, string>> GetPackageNamesByIdsAsync(IReadOnlyCollection<long> ids);
    Task<List<PartDemandSnapshot>> GetMandatoryPartDemandsAsync(long vehicleModelId, IReadOnlyCollection<long> serviceIds);
    Task<List<MandatoryPartBomItem>> GetMandatoryServiceBomsAsync(IReadOnlyCollection<long> vehicleModelIds, IReadOnlyCollection<long> serviceIds);
    Task<Dictionary<long, decimal>> GetAvailableInventoryByProductAsync(long branchId, IReadOnlyCollection<long> productIds);
    Task<Dictionary<(long BranchId, long ProductId), decimal>> GetAvailableInventoryByBranchAndProductAsync(IReadOnlyCollection<long> branchIds, IReadOnlyCollection<long> productIds);
    Task<Dictionary<long, ProductLabelItem>> GetProductLabelsByIdsAsync(IReadOnlyCollection<long> productIds);
    Task<List<MyGarage.Models.Inventory>> GetBranchInventoriesForProductAsync(long branchId, long productId, bool onlyWithReserved = false);
    Task<List<PartInventoryReservation>> GetActivePartReservationsForSlotsAsync(IReadOnlyCollection<long> slotIds);
    Task<Dictionary<long, decimal>> GetReservedPartQuantitiesForSlotsAsync(IReadOnlyCollection<long> slotIds);
}
