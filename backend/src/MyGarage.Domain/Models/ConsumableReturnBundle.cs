using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ConsumableReturnBundle
{
    public long Id { get; set; }
    public string BundleCode { get; set; } = null!;
    public long ServiceOrderId { get; set; }
    public long TechnicianId { get; set; }
    public DateTime ReturnedAt { get; set; }
    public long? ReceivedBy { get; set; }
    public string Status { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;
    public virtual Staff Technician { get; set; } = null!;
    public virtual Staff? ReceivedByStaff { get; set; }
    public virtual ICollection<ConsumableReturnBundleItem> Items { get; set; } = new List<ConsumableReturnBundleItem>();
}
