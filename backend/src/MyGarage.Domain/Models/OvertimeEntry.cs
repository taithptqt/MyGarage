using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class OvertimeEntry
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string OvertimeType { get; set; } = null!;

    public decimal Hours { get; set; }

    public decimal RateMultiplier { get; set; }

    public decimal Amount { get; set; }

    public long? ApprovedBy { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
