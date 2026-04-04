using System;
namespace MyGarage.DTOs.Responses
{
    public class PaymentMethodResponseDto
    {
        public long Id { get; set; }
        public string? MethodName { get; set; }
        public string? MethodCode { get; set; }
        public string? ProviderInfo { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
