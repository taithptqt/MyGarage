using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Responses;

public sealed class QuotationResponseDto
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public long ServiceOrderId { get; set; }
    public long? DiagnosisId { get; set; }
    public long? DiagnosisAssignmentId { get; set; }
    public string? Title { get; set; }
    public string QuotationType { get; set; } = null!;
    public int VersionNo { get; set; }
    public string Status { get; set; } = null!;
    public string? InsuranceClaimNumber { get; set; }
    [JsonPropertyName("insuranceApprovalEvidenceUrl")]
    public string? InsuranceApprovalEvidenceUrl { get; set; }
    public long? TechnicalApprovedBy { get; set; }
    public DateTime? TechnicalApprovedAt { get; set; }
    public string? TechnicalApprovalStatus { get; set; }
    public bool RequiresFinancialApproval { get; set; }
    public long? FinancialApprovedBy { get; set; }
    public DateTime? FinancialApprovedAt { get; set; }
    public string? FinancialApprovalStatus { get; set; }
    public long? CustomerDecisionBy { get; set; }
    public DateTime? CustomerDecisionAt { get; set; }
    public string? CustomerDecisionStatus { get; set; }
    public DateTime? ValidUntil { get; set; }
    public decimal SubTotalAmount { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal TotalInsuranceAmount { get; set; }
    public decimal TotalCustomerAmount { get; set; }
    public string InsuranceChangeType { get; set; } = null!;
    public List<string> Warnings { get; set; } = new();
    public bool IsAppliedToServiceOrder { get; set; }
    public bool IsReadyForSplitBilling { get; set; }
    public DateTime? SplitBillingReadyAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public List<QuotationDiscountResponseDto> Discounts { get; set; } = new();
    public List<QuotationDetailResponseDto> Details { get; set; } = new();
}

public sealed class QuotationDetailResponseDto
{
    public long Id { get; set; }
    public long? SourceQuotationDetailId { get; set; }
    public long? SourceQuotationId { get; set; }
    public string ServiceSource { get; set; } = "INITIAL";
    public bool IsSupplementary { get; set; }
    public string? SupplementaryReasonCode { get; set; }
    public string ItemType { get; set; } = null!;
    public long? ItemId { get; set; }
    public string ItemName { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceAtTime { get; set; }
    public decimal? CostPriceAtTime { get; set; }
    public decimal SubTotal { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string PayerType { get; set; } = null!;
    public long? InsuranceCompanyId { get; set; }
    public decimal? InsuranceCoveredAmount { get; set; }
    public decimal? CustomerCoveredAmount { get; set; }
    public string ApprovalStatus { get; set; } = null!;
    public string? RejectReason { get; set; }
    public string Status { get; set; } = null!;
    public string? CancelReason { get; set; }
    public long? CanceledBy { get; set; }
    public bool IsBlocking { get; set; }
    public bool IsMandatory { get; set; }
    public int? EstimatedMinutes { get; set; }
    public bool IsReturnOldPart { get; set; }
}
