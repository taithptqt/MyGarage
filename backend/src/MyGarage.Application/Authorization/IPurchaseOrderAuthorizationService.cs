using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IPurchaseOrderAuthorizationService
{
    Task<bool> CanViewPurchaseOrdersAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewPurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCreatePurchaseOrderAsync(long branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanUpdatePurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanApprovePurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanReceiveGoodsAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCancelOrDeletePurchaseOrderAsync(long purchaseOrderId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewGoodsReceiptsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewGoodsReceiptAsync(long goodsReceiptId, UserActorContext actor, CancellationToken ct = default);
}
