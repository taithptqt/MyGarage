using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceRefundUpdateDto
    {
        public decimal? RefundAmount { get; set; }
        public string? Reason { get; set; }
    }
}
