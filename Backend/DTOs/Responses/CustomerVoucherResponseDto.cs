using System;
namespace MyGarage.DTOs.Responses
{
    public class CustomerVoucherResponseDto
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public long VoucherId { get; set; }
        public bool? IsUsed { get; set; }
        public DateTime? UsedAt { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? RetailOrderId { get; set; }
    }
}
