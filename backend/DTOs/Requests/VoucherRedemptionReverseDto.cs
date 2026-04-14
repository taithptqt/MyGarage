namespace MyGarage.DTOs.Requests
{
    public class VoucherRedemptionReverseDto
    {
        public long? ApprovedBy { get; set; }
        public long? ApprovalRequestId { get; set; }
        public string? Reason { get; set; }
    }
}

