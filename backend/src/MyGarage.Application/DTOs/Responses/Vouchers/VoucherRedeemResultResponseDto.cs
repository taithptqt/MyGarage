namespace MyGarage.DTOs.Responses
{
    public class VoucherRedeemResultResponseDto
    {
        public bool Success { get; set; }
        public bool RequiresApproval { get; set; }
        public string? ReasonCode { get; set; }
        public string? Message { get; set; }
        public VoucherRedemptionResponseDto? Redemption { get; set; }
    }
}

