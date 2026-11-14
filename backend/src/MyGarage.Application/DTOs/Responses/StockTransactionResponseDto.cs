using System;
namespace MyGarage.DTOs.Responses
{
    public class StockTransactionResponseDto
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public long WarehouseId { get; set; }
        public string? Type { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Price { get; set; }
        public string? Note { get; set; }
        public long? RetailOrderId { get; set; }
        public long? RetailReturnId { get; set; }
        public long? MaterialRequestId { get; set; }
        public long? MaterialReturnId { get; set; }
        public long? PurchaseOrderId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
    }
}
