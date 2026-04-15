using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationSplitBillingUpdateRequestDto
{
    public long ActorStaffId { get; set; }
    public string? Note { get; set; }
    public List<QuotationSplitBillingUpdateItemDto> Items { get; set; } = new();
}

public sealed class QuotationSplitBillingUpdateItemDto
{
    public long QuotationDetailId { get; set; }
    public decimal? InsuranceCoveredAmount { get; set; }
    public string? PayerType { get; set; }
}
