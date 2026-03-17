using System;
namespace MyGarage.DTOs.Requests
{
    public class PaymentMethodCreateDto
    {
        public string? MethodName { get; set; }
        public string? MethodCode { get; set; }
        public string? ProviderInfo { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
