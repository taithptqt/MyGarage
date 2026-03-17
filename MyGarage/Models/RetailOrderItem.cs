using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class RetailOrderItem
{
    public long Id { get; set; }

    public long RetailOrderId { get; set; }

    public long ProductId { get; set; }

    public decimal Quantity { get; set; }

    public decimal? CostPriceAtTime { get; set; }

    public decimal UnitPriceAtTime { get; set; }

    public decimal SubTotal { get; set; }

    public decimal? TaxRate { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual RetailOrder RetailOrder { get; set; } = null!;

    public virtual ICollection<RetailReturnItem> RetailReturnItems { get; set; } = new List<RetailReturnItem>();
}
