using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class StockTransaction : IAuditEntity
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public long WarehouseId { get; set; }

    public string Type { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal? Price { get; set; }

    public string? Note { get; set; }

    public long? RetailOrderId { get; set; }

    public long? RetailReturnId { get; set; }

    public long? MaterialRequestId { get; set; }

    public long? MaterialReturnId { get; set; }

    public long? PurchaseOrderId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public long? CreatedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual MaterialReturn? MaterialReturn { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    public virtual RetailOrder? RetailOrder { get; set; }

    public virtual RetailReturn? RetailReturn { get; set; }
}
