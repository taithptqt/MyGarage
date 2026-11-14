using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationApproveRequestDto
{
    public long ApprovedBy { get; set; }
    public string? Note { get; set; }
    public List<QuotationDecisionDto> Decisions { get; set; } = new();
}

public sealed class QuotationDecisionDto
{
    public long QuotationDetailId { get; set; }
    public bool Approved { get; set; }
    public string? RejectReason { get; set; }
}
