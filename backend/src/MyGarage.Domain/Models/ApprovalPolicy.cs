using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ApprovalPolicy
{
    public long Id { get; set; }

    public string PolicyCode { get; set; } = null!;

    public string PolicyName { get; set; } = null!;

    public string TriggerType { get; set; } = null!;

    public decimal? ThresholdAmount { get; set; }

    public string RequiredApproverRole { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
