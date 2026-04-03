using System;
using System.Collections.Generic;

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

    public string ValidationStatus { get; set; } = null!;

    public string RedemptionStatus { get; set; } = null!;

    public string? FailReasonCode { get; set; }

    public decimal EligibleAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal BudgetImpact { get; set; }

    public long? AppliedBy { get; set; }

    public long? ApprovedBy { get; set; }

    public long? ApprovalRequestId { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTime AppliedAt { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public DateTime? ReversedAt { get; set; }

    public string? ReverseReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Staff? AppliedByNavigation { get; set; }

    public virtual VoucherApprovalRequest? ApprovalRequest { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Customer? Customer { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual PaymentMethod? PaymentMethod { get; set; }

    public virtual RetailOrder? RetailOrder { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual Vehicle? Vehicle { get; set; }

    public virtual Voucher Voucher { get; set; } = null!;

    public virtual ICollection<VoucherApprovalRequest> VoucherApprovalRequests { get; set; } = new List<VoucherApprovalRequest>();
}
