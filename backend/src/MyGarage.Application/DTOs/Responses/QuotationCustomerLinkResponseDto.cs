using System;

namespace MyGarage.DTOs.Responses;

public sealed class QuotationCustomerLinkResponseDto
{
    public long QuotationId { get; set; }
    public string QuotationCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PublicLink { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool EmailSent { get; set; }
    public string? SentToEmail { get; set; }
}

