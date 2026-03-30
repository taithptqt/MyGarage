namespace MyGarage.DTOs.Responses
{
    public class VoucherValidationResultResponseDto
    {
        public bool IsEligible { get; set; }
        public bool RequiresApproval { get; set; }
        public long? VoucherId { get; set; }
        public string? VoucherCode { get; set; }
        public string? ReasonCode { get; set; }
        public string? Message { get; set; }
        public decimal? EligibleAmount { get; set; }
        public decimal? DiscountPreview { get; set; }
    }
}

