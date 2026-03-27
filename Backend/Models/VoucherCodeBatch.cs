// Mục đích: Entity EF lưu metadata cho các đợt sinh/import mã voucher.
using System;

namespace MyGarage.Models;

public partial class VoucherCodeBatch
{
    public long Id { get; set; }

    public long VoucherId { get; set; }

    public string BatchCode { get; set; } = null!;

    public string? SourceType { get; set; }

    public int? TotalCount { get; set; }

    public int? SuccessCount { get; set; }

    public int? DuplicateCount { get; set; }

    public int? InvalidCount { get; set; }

    public string? FileName { get; set; }

    public string? Status { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}

