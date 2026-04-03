using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiFraudMonitor
{
    public long Id { get; set; }

    public string EntityType { get; set; } = null!;

    public long EntityId { get; set; }

    public string? RiskLevel { get; set; }

    public string? Reason { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }
}
