using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class BranchException
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public DateOnly ExceptionDate { get; set; }

    public bool? IsClosed { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Reason { get; set; }

    public long? CreatedByStaffId { get; set; }

    public long? ApprovedByStaffId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Staff? ApprovedByStaff { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff? CreatedByStaff { get; set; }
}
