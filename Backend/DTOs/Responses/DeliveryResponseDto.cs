using System;
namespace MyGarage.DTOs.Responses
{
    public class DeliveryResponseDto
    {
        public long Id { get; set; }
        public long RetailOrderId { get; set; }
        public string? ShippingAddress { get; set; }
        public string? CarrierName { get; set; }
        public string? TrackingCode { get; set; }
        public string? DeliveryEvidenceUrls { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
