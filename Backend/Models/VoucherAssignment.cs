// Mục đích: Entity EF ánh xạ voucher theo phạm vi áp dụng như chi nhánh hoặc nhóm khách hàng.
using System;

namespace MyGarage.Models;

public partial class VoucherAssignment
{
    public long Id { get; set; }

    public long VoucherId { get; set; }

    public string AssignmentType { get; set; } = null!;

    public long TargetId { get; set; }

    public bool? IsInclude { get; set; }

    public DateTime? EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}

