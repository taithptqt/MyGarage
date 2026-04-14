using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AiSmartDispatching
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public DateOnly Date { get; set; }

    public string? Recommendation { get; set; }

    public decimal? EfficiencyScore { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;
}
