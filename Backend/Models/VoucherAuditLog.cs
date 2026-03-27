// Mục đích: Entity EF lưu nhật ký kiểm toán riêng cho nghiệp vụ voucher.
using System;

namespace MyGarage.Models;

public partial class VoucherAuditLog
{
    public long Id { get; set; }

    public long? VoucherId { get; set; }

    public string EntityName { get; set; } = null!;

    public long EntityId { get; set; }

    public string Action { get; set; } = null!;

    public string? ReasonCode { get; set; }

    public string? BeforeData { get; set; }

    public string? AfterData { get; set; }

    public long? PerformedBy { get; set; }

    public long? BranchId { get; set; }

    public string? CorrelationId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Voucher? Voucher { get; set; }
}

