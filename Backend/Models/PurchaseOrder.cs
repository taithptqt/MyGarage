using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class PurchaseOrder
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public long DestinationWarehouseId { get; set; }

    public long SupplierId { get; set; }

    public DateOnly OrderDate { get; set; }

    public DateOnly? ExpectedDeliveryDate { get; set; }

    public decimal? TotalAmount { get; set; }

    public string Status { get; set; } = null!;

    public long? ApprovedByStaffId { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Staff? ApprovedByStaff { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Warehouse DestinationWarehouse { get; set; } = null!;

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();

    public virtual ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();

    public virtual Supplier Supplier { get; set; } = null!;
}
