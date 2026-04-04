using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class InventoryAuditDetail
{
    public long Id { get; set; }

    public long AuditId { get; set; }

    public long ProductId { get; set; }

    public decimal SystemQty { get; set; }

    public decimal ActualQty { get; set; }

    public decimal? DifferenceQty { get; set; }

    public string? Reason { get; set; }

    public virtual InventoryAudit Audit { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
