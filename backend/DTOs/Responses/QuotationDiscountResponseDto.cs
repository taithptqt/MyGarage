using System;

namespace MyGarage.DTOs.Responses;

public sealed class QuotationDiscountResponseDto
{
    public long Id { get; set; }
    public long QuotationId { get; set; }
    public string DiscountType { get; set; } = null!;
    public long? CustomerTierId { get; set; }
    public long? VoucherId { get; set; }
    public decimal DiscountValue { get; set; }
    public string ValueType { get; set; } = null!;
    public decimal AppliedAmount { get; set; }
    public string? Reason { get; set; }
    public long AppliedBy { get; set; }
    public int? DiscountCategory { get; set; }
    public DateTime? CreatedAt { get; set; }
}
