using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceRefundCreateDto
    {
        public long ServiceOrderId { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? Reason { get; set; }
    }
}
