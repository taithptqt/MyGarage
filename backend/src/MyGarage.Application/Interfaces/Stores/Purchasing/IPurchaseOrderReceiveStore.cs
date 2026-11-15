using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Purchasing;

public interface IPurchaseOrderReceiveStore
{
    Task<PurchaseOrder?> GetPurchaseOrderWithItemsAsync(long purchaseOrderId);
    Task<bool> WarehouseExistsAsync(long warehouseId);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action);
    Task AddGoodsReceiptNoteAndSaveAsync(GoodsReceiptNote grn);
    Task<MyGarage.Models.Inventory?> GetInventoryAsync(long warehouseId, long productId);
    Task<decimal> GetProductMinStockLevelAsync(long productId);
    void AddInventory(MyGarage.Models.Inventory inventory);
    void AddStockTransaction(StockTransaction stockTransaction);
    void AddGoodsReceiptNoteItems(IEnumerable<GoodsReceiptNoteItem> items);
    Task SaveChangesAsync();
    Task<List<long>> GetUnresolvedStockAlertIdsAsync(long warehouseId, IReadOnlyCollection<long> productIds);
}
