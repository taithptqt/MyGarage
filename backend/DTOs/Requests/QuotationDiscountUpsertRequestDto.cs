using System.Collections.Generic;

namespace MyGarage.DTOs.Requests;

public sealed class QuotationDiscountUpsertRequestDto
{
    public long ActorStaffId { get; set; }
    public string? Note { get; set; }
    public List<QuotationDiscountUpsertItemDto> Items { get; set; } = new();
}

public sealed class QuotationDiscountUpsertItemDto
{
    public string? DiscountType { get; set; }
    public long? CustomerTierId { get; set; }
    public long? VoucherId { get; set; }
    public string? VoucherCode { get; set; }
    public decimal? DiscountValue { get; set; }
    public string? ValueType { get; set; }
    public decimal? AppliedAmount { get; set; }
    public string? Reason { get; set; }
}
