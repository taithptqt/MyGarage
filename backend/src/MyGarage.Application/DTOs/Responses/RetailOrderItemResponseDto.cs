using System;
namespace MyGarage.DTOs.Responses
{
    public class RetailOrderItemResponseDto
    {
        public long Id { get; set; }
        public long RetailOrderId { get; set; }
        public long ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductSku { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? CostPriceAtTime { get; set; }
        public decimal? UnitPriceAtTime { get; set; }
        public decimal? SubTotal { get; set; }
        public decimal? TaxRate { get; set; }
        public decimal? TaxAmount { get; set; }
        public decimal? FinalAmount { get; set; }
    }
}
