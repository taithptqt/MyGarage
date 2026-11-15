using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Purchasing.DTOs;

namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderGoodsReceiptService
{
    Task<PurchaseOrderReceiveResultDto> ReceivePurchaseOrderAsync(
        long id,
        PurchaseOrderReceiveInput input,
        UserActorContext actor,
        CancellationToken ct = default);

    Task<PurchaseOrderReceiveHistoryDto> GetReceiveHistoryAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default);
}
