using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class BayAllocation
{
    public long Id { get; set; }
    public long BayId { get; set; }
    public long ServiceOrderId { get; set; }
    public long VehicleId { get; set; }
    public DateTime AllocatedAt { get; set; }
    public long AllocatedBy { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public long? ReleasedBy { get; set; }
    public int? PlannedDuration { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Bay Bay { get; set; } = null!;
    public virtual ServiceOrder ServiceOrder { get; set; } = null!;
    public virtual Vehicle Vehicle { get; set; } = null!;
    public virtual Staff AllocatedByStaff { get; set; } = null!;
    public virtual Staff? ReleasedByStaff { get; set; }
}
