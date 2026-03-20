using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceOrderDiscountCreateDto
    {
        public long ServiceOrderId { get; set; }
        public string? DiscountType { get; set; }
        public long? CustomerTierId { get; set; }
        public long? VoucherId { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? ValueType { get; set; }
        public decimal? AppliedAmount { get; set; }
        public string? Reason { get; set; }
        public long AppliedBy { get; set; }
        public int? DiscountCategory { get; set; }
    }
}
