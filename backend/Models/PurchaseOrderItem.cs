using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class PurchaseOrderItem
{
    public long Id { get; set; }

    public long PurchaseOrderId { get; set; }

    public long ProductId { get; set; }

    public decimal OrderedQuantity { get; set; }

    public decimal? ReceivedQuantity { get; set; }

    public decimal CostPrice { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;

    public virtual ICollection<SupplierInvoiceDetail> SupplierInvoiceDetails { get; set; } = new List<SupplierInvoiceDetail>();
}
