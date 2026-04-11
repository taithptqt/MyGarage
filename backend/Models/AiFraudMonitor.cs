using System;

namespace MyGarage.Models;

public class AiFraudMonitor
{
    public long Id { get; set; }

    public string? EntityType { get; set; }

    public string? RiskLevel { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }
}
