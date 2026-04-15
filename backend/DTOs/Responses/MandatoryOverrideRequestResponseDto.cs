using System;

namespace MyGarage.DTOs.Responses;

public sealed class MandatoryOverrideRequestResponseDto
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
    public string? QuotationCode { get; set; }
    public int? QuotationVersionNo { get; set; }
    public string? QuotationDetailItemName { get; set; }
    public bool QuotationDetailIsMandatory { get; set; }
}
