using System;

namespace MyGarage.Models;

public partial class PartInventoryReservation
{
    public long Id { get; set; }

    public long WorkSlotId { get; set; }

    public long ProductId { get; set; }

    public decimal ReservedQuantity { get; set; }

    public string Status { get; set; } = null!;

    public DateTime ExpiryTime { get; set; }

    public DateTime CreateAt { get; set; }

    public long? CreateBy { get; set; }

    public DateTime UpdateAt { get; set; }

    public long? UpdateBy { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual WorkSlot WorkSlot { get; set; } = null!;

    public virtual Staff? CreateByNavigation { get; set; }

    public virtual Staff? UpdateByNavigation { get; set; }
}

