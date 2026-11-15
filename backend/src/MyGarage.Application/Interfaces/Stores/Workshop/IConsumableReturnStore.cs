using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record ConsumableProductInfo(long Id, string? Unit, bool? IsConsumable);

public interface IConsumableReturnStore
{
    Task<bool> ServiceOrderExistsAsync(long serviceOrderId);
    Task<bool> TechnicianExistsAsync(long technicianId);
    void AddBundle(ConsumableReturnBundle bundle);
    Task<Dictionary<long, ConsumableProductInfo>> GetProductsByIdsAsync(IReadOnlyCollection<long> productIds);
    void AddBundleItem(ConsumableReturnBundleItem item);
    Task<MaterialRequestDetail?> GetMaterialRequestDetailForUpdateAsync(long materialReqDetailId);
    Task<MyGarage.Models.Inventory?> GetInventoryByProductForUpdateAsync(long productId);
    void AddStockTransaction(StockTransaction transaction);
    Task<IReadOnlyList<ConsumableReturnBundleItem>> GetBundleItemsAsync(long bundleId);
    Task SaveChangesInTransactionAsync();
}
