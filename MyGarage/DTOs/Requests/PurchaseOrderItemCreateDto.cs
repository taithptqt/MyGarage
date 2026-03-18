using System;
namespace MyGarage.DTOs.Requests
{
    public class PurchaseOrderItemCreateDto
    {
        public long PurchaseOrderId { get; set; }
        public long ProductId { get; set; }
        public decimal? OrderedQuantity { get; set; }
        public decimal? ReceivedQuantity { get; set; }
        public decimal? CostPrice { get; set; }
    }
}
