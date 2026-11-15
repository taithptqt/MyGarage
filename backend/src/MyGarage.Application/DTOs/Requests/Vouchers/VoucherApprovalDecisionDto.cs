namespace MyGarage.DTOs.Requests
{
    public class VoucherApprovalDecisionDto
    {
        public long ApproverId { get; set; }
        public string? Note { get; set; }
        public string? Reason { get; set; }
    }
}

