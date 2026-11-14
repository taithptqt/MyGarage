using System;

namespace MyGarage.Models;

public partial class StockAlert
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long WarehouseId { get; set; }
    public string AlertType { get; set; } = null!;
    public decimal CurrentStock { get; set; }
    public decimal MinStock { get; set; }
    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public long? ResolvedByPurchaseOrderId { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual PurchaseOrder? ResolvedByPurchaseOrder { get; set; }
}
