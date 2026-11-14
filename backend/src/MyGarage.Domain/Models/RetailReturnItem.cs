using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class RetailReturnItem
{
    public long Id { get; set; }

    public long RetailReturnId { get; set; }

    public long RetailOrderItemId { get; set; }

    public long WarehouseId { get; set; }

    public decimal ReturnedQuantity { get; set; }

    public string ItemCondition { get; set; } = null!;

    public decimal RefundAmount { get; set; }

    public string? Status { get; set; }

    public string? Note { get; set; }

    public string? ConditionEvidenceUrls { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff CreatedByNavigation { get; set; } = null!;

    public virtual RetailOrderItem RetailOrderItem { get; set; } = null!;

    public virtual RetailReturn RetailReturn { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
