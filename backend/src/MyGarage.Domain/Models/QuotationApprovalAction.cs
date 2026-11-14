using System;

namespace MyGarage.Models;

public partial class QuotationApprovalAction
{
    public long Id { get; set; }

    public long QuotationId { get; set; }

    public string ApprovalType { get; set; } = null!;

    public string Decision { get; set; } = null!;

    public long ActorStaffId { get; set; }

    public string? Note { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Quotation Quotation { get; set; } = null!;
}
