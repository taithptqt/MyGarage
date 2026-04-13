using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationCreateRequestDto
{
    public string? Title { get; set; }
    public string? QuotationType { get; set; }
    public DateTime? ValidUntil { get; set; }
    public long? ServiceAdvisorId { get; set; }
    public string? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }
    public bool SubmitForApproval { get; set; }
    public List<QuotationCreateItemDto> Items { get; set; } = new();
}

public sealed class QuotationCreateItemDto
{
    public long? DiagnoseRecommendationId { get; set; }
    public string ItemType { get; set; } = null!;
    public long? ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPriceAtTime { get; set; }
    public decimal? CostPriceAtTime { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal TaxRate { get; set; }
    public string? PayerType { get; set; }
    public long? InsuranceCompanyId { get; set; }
    public decimal? InsuranceRequestedAmount { get; set; }
    public decimal? CustomerCoPayAmount { get; set; }
    public bool IsBlocking { get; set; } = true;
    public int? EstimatedMinutes { get; set; }
    public bool IsReturnOldPart { get; set; }
}
