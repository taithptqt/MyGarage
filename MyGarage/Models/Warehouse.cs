using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Warehouse : IAuditEntity
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Type { get; set; } = null!;

    public long? ManagerId { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

    public virtual ICollection<InventoryAudit> InventoryAudits { get; set; } = new List<InventoryAudit>();

    public virtual Staff? Manager { get; set; }

    public virtual ICollection<MaterialRequestDetail> MaterialRequestDetails { get; set; } = new List<MaterialRequestDetail>();

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();

    public virtual ICollection<RetailReturnItem> RetailReturnItems { get; set; } = new List<RetailReturnItem>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
