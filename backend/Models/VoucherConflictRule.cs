using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VoucherConflictRule
{
    public long Id { get; set; }

    public long VoucherId { get; set; }

    public string ConflictType { get; set; } = null!;

    public string? ConflictTarget { get; set; }

    public string ActionType { get; set; } = null!;

    public string? MessageCode { get; set; }

    public string? MessageText { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}
