namespace MyGarage.DTOs.OData;

public sealed class InvoiceODataDto
{
    public long Id { get; set; }
    public long? ServiceOrderId { get; set; }
    public long? RetailOrderId { get; set; }
    public long? BranchId { get; set; }
    public string PayerType { get; set; } = string.Empty;
    public long? PayerId { get; set; }
    public decimal? SubTotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? PaidAmount { get; set; }
    public string? PaymentStatus { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
}
