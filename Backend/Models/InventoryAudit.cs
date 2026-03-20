using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class InventoryAudit
{
    public long Id { get; set; }

    public long WarehouseId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime AuditDate { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<InventoryAuditDetail> InventoryAuditDetails { get; set; } = new List<InventoryAuditDetail>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual Warehouse Warehouse { get; set; } = null!;
}
