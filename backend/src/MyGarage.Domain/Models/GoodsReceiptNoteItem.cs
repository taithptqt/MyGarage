using System;

namespace MyGarage.Models;

public partial class GoodsReceiptNoteItem
{
    public long Id { get; set; }
    public long GoodsReceiptNoteId { get; set; }
    public long PurchaseOrderItemId { get; set; }
    public long ProductId { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineAmount { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public virtual PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
