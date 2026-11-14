using System;

namespace MyGarage.Models;

public partial class CapacityConfig
{
    public long Id { get; set; }

    public long? BranchId { get; set; }

    public byte DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int MaxBaysAvailable { get; set; }

    public decimal? BookingThresholdPercent { get; set; }

    public int? ActiveTechnicians { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreateAt { get; set; }

    public long? CreateBy { get; set; }

    public DateTime UpdateAt { get; set; }

    public long? UpdateBy { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Staff? CreateByNavigation { get; set; }

    public virtual Staff? UpdateByNavigation { get; set; }
}
