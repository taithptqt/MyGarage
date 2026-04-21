using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationCreateRequestDto
{
    public long? DiagnosisId { get; set; }
    public long? DiagnosisAssignmentId { get; set; }

    [StringLength(200)]
    public string? Title { get; set; }

    [StringLength(50)]
    public string? QuotationType { get; set; }

    public DateTime? ValidUntil { get; set; }
    public long? ServiceAdvisorId { get; set; }

    [StringLength(100)]
    public string? InsuranceClaimNumber { get; set; }

    [StringLength(2000)]
    public string? CustomerNotes { get; set; }

    [StringLength(2000)]
    public string? InternalNotes { get; set; }

    public bool SubmitForApproval { get; set; }
    public List<QuotationCreateItemDto> Items { get; set; } = new();
}

public sealed class QuotationCreateItemDto
{
    public long? SourceQuotationDetailId { get; set; }
    public long? DiagnosisRecommendationId { get; set; }

    [JsonPropertyName("diagnoseRecommendationId")]
    public long? DiagnoseRecommendationId { get; set; }

    [Required]
    [StringLength(50)]
    public string ItemType { get; set; } = null!;

    public long? ItemId { get; set; }

    [Required]
    [StringLength(500)]
    public string ItemName { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(0, 1000000)]
    public decimal Quantity { get; set; } = 1;

    [Range(0, 1000000000)]
    public decimal UnitPriceAtTime { get; set; }

    [Range(0, 1000000000)]
    public decimal? CostPriceAtTime { get; set; }

    [Range(0, 1000000000)]
    public decimal DiscountValue { get; set; }

    [Range(0, 100)]
    public decimal TaxRate { get; set; }

    [StringLength(50)]
    public string? PayerType { get; set; }

    public long? InsuranceCompanyId { get; set; }

    [Range(0, 1000000000)]
    public decimal? InsuranceCoveredAmount { get; set; }

    [Range(0, 1000000000)]
    public decimal? CustomerCoveredAmount { get; set; }

    public bool IsBlocking { get; set; } = true;
    public bool IsMandatory { get; set; }
    public int? EstimatedMinutes { get; set; }
    public bool IsReturnOldPart { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [StringLength(1000)]
    public string? CancelReason { get; set; }

    public long? CanceledBy { get; set; }
}
