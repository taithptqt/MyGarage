using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Inventory.DTOs;
using MyGarage.Models;

namespace MyGarage.Application.Inventory;

public interface IInventoryOperationsStore
{
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    Task<bool> PurchaseOrderExistsAsync(long purchaseOrderId, CancellationToken ct = default);

    Task<(IReadOnlyList<StockTransactionItemDto> Items, int TotalCount)> GetStockTransactionsPageAsync(
        long? branchId,
        long? warehouseId,
        long? productId,
        string? normalizedType,
        DateTime? from,
        DateTime? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    // Authorization
    Task<long?> GetWarehouseBranchIdAsync(long warehouseId, CancellationToken ct = default);
    Task<(bool Found, long? ServiceOrderId)> GetMaterialRequestServiceOrderIdAsync(long materialRequestId, CancellationToken ct = default);
    Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken ct = default);

    // GRN Snapshot
    Task<IReadOnlyList<GoodsReceiptNote>> GetGoodsReceiptNotesForSnapshotAsync(DateTime start, DateTime end, long? branchId, CancellationToken ct = default);
    Task<IReadOnlyList<GoodsReceiptNoteItem>> GetGoodsReceiptNoteItemsAsync(IReadOnlyCollection<long> grnIds, CancellationToken ct = default);
    Task<GrnDailySnapshot?> GetGrnDailySnapshotAsync(long branchId, DateOnly snapshotDate, CancellationToken ct = default);
    void AddGrnDailySnapshot(GrnDailySnapshot snapshot);
    void RemoveGrnDailySnapshot(GrnDailySnapshot snapshot);
    Task<IReadOnlyList<GrnDailySnapshot>> GetGrnDailySnapshotsRangeAsync(DateOnly fromDate, DateOnly toDate, long? branchId, CancellationToken ct = default);

    // Material Request / Return
    Task<Quotation?> GetQuotationWithDetailsAndServiceOrderAsync(long quotationId, CancellationToken ct = default);
    Task<MaterialRequest?> GetMaterialRequestByIdAsync(long requestId, CancellationToken ct = default);
    Task<IReadOnlyList<MaterialRequestDetail>> GetMaterialRequestDetailsAsync(long requestId, CancellationToken ct = default);
    Task<MyGarage.Models.Inventory?> GetTrackedInventoryAsync(long productId, long warehouseId, CancellationToken ct = default);
}
