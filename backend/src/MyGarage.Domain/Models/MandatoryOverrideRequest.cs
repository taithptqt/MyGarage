using System;

namespace MyGarage.Models;

public partial class MandatoryOverrideRequest
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public long QuotationId { get; set; }

    public long QuotationDetailId { get; set; }

    public string Status { get; set; } = null!;

    public string? Reason { get; set; }

    public long RequestedBy { get; set; }

    public DateTime RequestedAt { get; set; }

    public long? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionNote { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Quotation Quotation { get; set; } = null!;

    public virtual QuotationDetail QuotationDetail { get; set; } = null!;
}
