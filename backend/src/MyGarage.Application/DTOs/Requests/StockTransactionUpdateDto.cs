using System;
namespace MyGarage.DTOs.Requests
{
    public class StockTransactionUpdateDto
    {
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
        public long? CreatedBy { get; set; }
    }
}
