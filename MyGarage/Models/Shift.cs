using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Shift
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public string ShiftName { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual ICollection<ShiftClosing> ShiftClosings { get; set; } = new List<ShiftClosing>();

    public virtual ICollection<StaffSchedule> StaffSchedules { get; set; } = new List<StaffSchedule>();
}
