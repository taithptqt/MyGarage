// Mục đích: Entity EF lưu giao dịch kiểm tra, redeem và hoàn tác voucher.
using System;

namespace MyGarage.Models;

public partial class VoucherRedemption
{
    public long Id { get; set; }

    public long VoucherId { get; set; }

    public string VoucherCode { get; set; } = null!;

    public long? CustomerId { get; set; }

    public long? VehicleId { get; set; }

    public long? BranchId { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? RetailOrderId { get; set; }

    public long? InvoiceId { get; set; }

    public long? PaymentMethodId { get; set; }

    public string? ValidationStatus { get; set; }

    public string? RedemptionStatus { get; set; }

    public string? FailReasonCode { get; set; }

    public decimal? EligibleAmount { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal? BudgetImpact { get; set; }

    public long? AppliedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public long? ApprovalRequestId { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTime? AppliedAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public DateTime? ReversedAt { get; set; }

    public string? ReverseReason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;

    public virtual VoucherApprovalRequest? ApprovalRequest { get; set; }
}

