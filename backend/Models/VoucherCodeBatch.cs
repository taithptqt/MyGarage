using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VoucherCodeBatch
{
    public long Id { get; set; }

    public long VoucherId { get; set; }

    public string BatchCode { get; set; } = null!;

    public string SourceType { get; set; } = null!;

    public int TotalCount { get; set; }

    public int SuccessCount { get; set; }

    public int DuplicateCount { get; set; }

    public int InvalidCount { get; set; }

    public string? FileName { get; set; }

    public string Status { get; set; } = null!;

    public long? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;
}
