using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class InvoiceDetail
{
    public long Id { get; set; }

    public long InvoiceId { get; set; }

    public long? ReferenceServiceOrderDetailId { get; set; }

    public string ItemType { get; set; } = null!;

    public long? ProductId { get; set; }

    public long? StandardServiceId { get; set; }

    public string ItemName { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal? CostPriceAtTime { get; set; }

    public decimal UnitPriceAtTime { get; set; }

    public string Unit { get; set; } = null!;

    public decimal SubTotal { get; set; }

    public decimal? DiscountAllocatedAmount { get; set; }

    public decimal? TaxRate { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string? Note { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual Product? Product { get; set; }

    public virtual StandardService? StandardService { get; set; }
}
