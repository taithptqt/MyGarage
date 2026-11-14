using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class GoodsReceiptNote
{
    public long Id { get; set; }
    public string GrnCode { get; set; } = null!;
    public long PurchaseOrderId { get; set; }
    public long WarehouseId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public long? ReceivedBy { get; set; }
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual Staff? ReceivedByStaff { get; set; }
    public virtual ICollection<GoodsReceiptNoteItem> Items { get; set; } = new List<GoodsReceiptNoteItem>();
}
