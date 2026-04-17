using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses;

public sealed class QuotationPublicViewResponseDto
{
    public long QuotationId { get; set; }
    public string Code { get; set; } = null!;
    public string? Title { get; set; }
    public int VersionNo { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? ValidUntil { get; set; }
    public string CustomerName { get; set; } = "-";
    public string? CustomerEmail { get; set; }
    public string VehicleLicensePlate { get; set; } = "-";
    public decimal FinalAmount { get; set; }
    public decimal TotalInsuranceAmount { get; set; }
    public decimal TotalCustomerAmount { get; set; }
    public bool CanApprove { get; set; }
    public bool CanReject { get; set; }
    public List<QuotationPublicDetailResponseDto> Details { get; set; } = new();
}

public sealed class QuotationPublicDetailResponseDto
{
    public long Id { get; set; }
    public long? SourceQuotationId { get; set; }
    public string ServiceSource { get; set; } = "INITIAL";
    public bool IsSupplementary { get; set; }
    public string? SupplementaryReasonCode { get; set; }
    public string ItemType { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal UnitPriceAtTime { get; set; }
    public decimal FinalAmount { get; set; }
    public bool IsMandatory { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? CancelReason { get; set; }
}

