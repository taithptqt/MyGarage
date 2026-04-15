using System;

namespace MyGarage.Models;

public partial class QuotationAccessLink
{
    public long Id { get; set; }

    public long QuotationId { get; set; }

    public string TokenHash { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ViewedAt { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionCode { get; set; }

    public string? SentToEmail { get; set; }

    public string? ClientNote { get; set; }

    public string? OtpCodeHash { get; set; }

    public DateTime? OtpExpiresAt { get; set; }

    public int OtpAttemptCount { get; set; }

    public DateTime? OtpVerifiedAt { get; set; }

    public DateTime? OtpLastSentAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Quotation Quotation { get; set; } = null!;
}
