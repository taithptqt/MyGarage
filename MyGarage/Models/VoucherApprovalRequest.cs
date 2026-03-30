using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VoucherApprovalRequest
{
    public long Id { get; set; }

    public string RequestType { get; set; } = null!;

    public long? VoucherId { get; set; }

    public long? RedemptionId { get; set; }

    public long? BranchId { get; set; }

    public long? ServiceOrderId { get; set; }

    public long? RetailOrderId { get; set; }

    public long? InvoiceId { get; set; }

    public long RequestedBy { get; set; }

    public long? ApproverId { get; set; }

    public string Status { get; set; } = null!;

    public string RiskLevel { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public string? RejectReason { get; set; }

    public string? RequestPayload { get; set; }

    public decimal? ImpactAmount { get; set; }

    public DateTime? Sladeadline { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? Approver { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual VoucherRedemption? Redemption { get; set; }

    public virtual Staff RequestedByNavigation { get; set; } = null!;

    public virtual RetailOrder? RetailOrder { get; set; }

    public virtual ServiceOrder? ServiceOrder { get; set; }

    public virtual Voucher? Voucher { get; set; }

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();
}
