using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public sealed record PurchaseOrderReceiveLineResult(
        long PurchaseOrderItemId,
        long ProductId,
        decimal AcceptedQuantity,
        decimal OrderedQuantity,
        decimal? TotalReceivedQuantity);

    public sealed record PurchaseOrderReceiveResult(
        string GoodsReceiptNoteCode,
        long GoodsReceiptNoteId,
        long PurchaseOrderId,
        string Status,
        System.DateTime ReceivedAt,
        long DestinationWarehouseId,
        decimal TotalReceivedAmount,
        IReadOnlyList<PurchaseOrderReceiveLineResult> Lines);

    public interface IPurchaseOrderService : IGenericService<PurchaseOrder>
    {
        Task<PurchaseOrderReceiveResult> ReceivePurchaseOrderAsync(
            long purchaseOrderId,
            PurchaseOrderReceiveRequestDto request,
            long? actorStaffId);
    }
}
