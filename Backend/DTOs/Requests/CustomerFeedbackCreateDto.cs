using System;
namespace MyGarage.DTOs.Requests
{
    public class CustomerFeedbackCreateDto
    {
        public long? ServiceOrderId { get; set; }
        public long? RetailOrderId { get; set; }
        public long CustomerId { get; set; }
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public string? ImageUrls { get; set; }
    }
}
