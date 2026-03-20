using System;
namespace MyGarage.DTOs.Requests
{
    public class DeliveryCreateDto
    {
        public long RetailOrderId { get; set; }
        public string? ShippingAddress { get; set; }
        public string? CarrierName { get; set; }
        public string? TrackingCode { get; set; }
        public string? DeliveryEvidenceUrls { get; set; }
        public string? Status { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
