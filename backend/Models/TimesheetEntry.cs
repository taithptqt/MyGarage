using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class TimesheetEntry
{
    public long Id { get; set; }
    public long StaffId { get; set; }
    public long BranchId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public int BreakMinutes { get; set; }
    public string WorkType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Staff Staff { get; set; } = null!;
    public virtual Branch Branch { get; set; } = null!;
    public virtual Staff? ApprovedByStaff { get; set; }
}
