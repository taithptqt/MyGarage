using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class AttendanceAdjustment
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string AdjustmentType { get; set; } = null!;

    public int? Minutes { get; set; }

    public decimal AmountImpact { get; set; }

    public string? Reason { get; set; }

    public long? ApprovedBy { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
