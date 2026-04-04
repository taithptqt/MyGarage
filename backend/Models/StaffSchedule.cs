using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class StaffSchedule
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public DateOnly Date { get; set; }

    public long ShiftId { get; set; }

    public DateTime? CheckInTime { get; set; }

    public DateTime? CheckOutTime { get; set; }

    public string? Status { get; set; }

    public virtual Shift Shift { get; set; } = null!;

    public virtual Staff Staff { get; set; } = null!;
}
