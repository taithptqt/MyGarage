using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Purchasing;

public interface IPurchaseOrderAuthorizationStore
{
    Task<long?> GetPurchaseOrderBranchIdAsync(long purchaseOrderId, CancellationToken ct = default);
    Task<long?> GetGoodsReceiptBranchIdAsync(long goodsReceiptId, CancellationToken ct = default);
}
