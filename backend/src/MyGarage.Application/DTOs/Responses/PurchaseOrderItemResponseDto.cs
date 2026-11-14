using System;
namespace MyGarage.DTOs.Responses
{
    public class PurchaseOrderItemResponseDto
    {
        public long Id { get; set; }
        public long PurchaseOrderId { get; set; }
        public long ProductId { get; set; }
        public decimal? OrderedQuantity { get; set; }
        public decimal? ReceivedQuantity { get; set; }
        public decimal? CostPrice { get; set; }
    }
}
