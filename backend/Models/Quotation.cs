using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Quotation
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public long ServiceOrderId { get; set; }

    public string? Title { get; set; }

    public string QuotationType { get; set; } = null!;

    public int VersionNo { get; set; }

    public decimal SubTotalAmount { get; set; }

    public decimal TotalDiscount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public decimal TotalInsuranceAmount { get; set; }

    public decimal TotalCustomerAmount { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? ValidUntil { get; set; }

    public long? ServiceAdvisorId { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }

    public string? CustomerNotes { get; set; }

    public string? InternalNotes { get; set; }

    public bool IsAppliedToServiceOrder { get; set; }

    public long? AppliedBy { get; set; }

    public DateTime? AppliedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Staff? ServiceAdvisor { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff? AppliedByNavigation { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<QuotationDetail> QuotationDetails { get; set; } = new List<QuotationDetail>();
}
