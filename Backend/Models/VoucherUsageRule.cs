// Mục đích: Entity EF định nghĩa bộ điều kiện đủ để áp dụng voucher.
using System;

namespace MyGarage.Models;

public partial class VoucherUsageRule
{
    public long Id { get; set; }

    public long VoucherId { get; set; }

    public string RuleType { get; set; } = null!;

    public string Operator { get; set; } = null!;

    public string RuleValue { get; set; } = null!;

    public bool? IsExclude { get; set; }

    public bool? HardBlock { get; set; }

    public int? Priority { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}

