using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Purchasing;

public interface IGoodsReceiptStore
{
    Task<(IReadOnlyList<GoodsReceiptNote> Items, int TotalCount)> GetGoodsReceiptsPageAsync(
        long? branchId,
        long? purchaseOrderId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<GoodsReceiptNote?> GetGoodsReceiptByIdAsync(long id, CancellationToken ct = default);

    Task<bool> PurchaseOrderExistsAsync(long purchaseOrderId, CancellationToken ct = default);
}
