using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class SupplierInvoiceDetail
{
    public long Id { get; set; }

    public long SupplierInvoiceId { get; set; }

    public long? PurchaseOrderItemId { get; set; }

    public long? ProductId { get; set; }

    public string ItemName { get; set; } = null!;

    public decimal Quantity { get; set; }

    public string? Unit { get; set; }

    public decimal UnitCost { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Product? Product { get; set; }

    public virtual PurchaseOrderItem? PurchaseOrderItem { get; set; }

    public virtual SupplierInvoice SupplierInvoice { get; set; } = null!;
}
