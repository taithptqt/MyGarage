using System;

namespace MyGarage.Models;

public partial class GrnDailySnapshot
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public int GrnCount { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public decimal TotalReceivedQuantity { get; set; }
    public int DistinctProducts { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Branch Branch { get; set; } = null!;
}
