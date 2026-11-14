using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class BayStatusLog
{
    public long Id { get; set; }
    public long BayId { get; set; }
    public string OldStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
    public string? Reason { get; set; }
    public long ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }

    public virtual Bay Bay { get; set; } = null!;
    public virtual Staff ChangedByStaff { get; set; } = null!;
}
