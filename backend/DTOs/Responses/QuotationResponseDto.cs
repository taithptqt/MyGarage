using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses;

public sealed class QuotationResponseDto
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public long ServiceOrderId { get; set; }
    public string? Title { get; set; }
    public string QuotationType { get; set; } = null!;
    public int VersionNo { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? ValidUntil { get; set; }
    public decimal SubTotalAmount { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal TotalInsuranceAmount { get; set; }
    public decimal TotalCustomerAmount { get; set; }
    public bool IsAppliedToServiceOrder { get; set; }
    public DateTime? AppliedAt { get; set; }
    public List<QuotationDetailResponseDto> Details { get; set; } = new();
}

public sealed class QuotationDetailResponseDto
{
    public long Id { get; set; }
    public long? DiagnoseRecommendationId { get; set; }
    public string ItemType { get; set; } = null!;
    public long? ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceAtTime { get; set; }
    public decimal? CostPriceAtTime { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string PayerType { get; set; } = null!;
    public long? InsuranceCompanyId { get; set; }
    public decimal? InsuranceRequestedAmount { get; set; }
    public decimal? CustomerCoPayAmount { get; set; }
    public string ApprovalStatus { get; set; } = null!;
    public string? RejectReason { get; set; }
    public bool IsBlocking { get; set; }
    public int? EstimatedMinutes { get; set; }
    public bool IsReturnOldPart { get; set; }
}
