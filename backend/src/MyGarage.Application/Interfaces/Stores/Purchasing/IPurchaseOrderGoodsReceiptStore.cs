using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderGoodsReceiptStore
{
    Task<bool> HasGoodsReceiptsForOrderAsync(long purchaseOrderId, CancellationToken ct = default);

    Task<int> CountGoodsReceiptsForOrderAsync(long purchaseOrderId, CancellationToken ct = default);

    Task AddGoodsReceiptNoteAndSaveAsync(GoodsReceiptNote grn, CancellationToken ct = default);

    Task<MyGarage.Models.Inventory?> GetInventoryAsync(long warehouseId, long productId, CancellationToken ct = default);

    Task<decimal> GetProductMinStockLevelAsync(long productId, CancellationToken ct = default);

    void AddInventory(MyGarage.Models.Inventory inventory);

    void AddStockTransaction(StockTransaction stockTransaction);

    void AddGoodsReceiptNoteItems(IEnumerable<GoodsReceiptNoteItem> items);

    Task<IReadOnlyList<long>> GetUnresolvedAlertIdsAsync(
        long warehouseId,
        IReadOnlyCollection<long> productIds,
        CancellationToken ct = default);

    Task<IReadOnlyList<GoodsReceiptNote>> GetGoodsReceiptsForOrderAsync(long purchaseOrderId, CancellationToken ct = default);

    Task<(IReadOnlyList<GoodsReceiptNote> Grns, IReadOnlyList<GoodsReceiptNoteItem> Items)> GetGrnKpiDataAsync(
        long? branchId,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);
}
