using System;
namespace MyGarage.DTOs.Requests
{
    public class InvoiceDetailUpdateDto
    {
        public long InvoiceId { get; set; }
        public long? ReferenceServiceOrderDetailId { get; set; }
        public string? ItemType { get; set; }
        public long? ProductId { get; set; }
        public long? StandardServiceId { get; set; }
        public string? ItemName { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? CostPriceAtTime { get; set; }
        public decimal? UnitPriceAtTime { get; set; }
        public string? Unit { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? DiscountAllocatedAmount { get; set; }
        public decimal? TaxRate { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? FinalAmount { get; set; }
        public string? Note { get; set; }
    }
}
