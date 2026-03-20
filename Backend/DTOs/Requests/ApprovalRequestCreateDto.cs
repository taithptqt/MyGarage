using System;
namespace MyGarage.DTOs.Requests
{
    public class ApprovalRequestCreateDto
    {
        public string? RequestType { get; set; }
        public long? ServiceOrderDiscountId { get; set; }
        public long? GatePassId { get; set; }
        public long? MaterialReturnId { get; set; }
        public long RequestedBy { get; set; }
        public long? ApproverId { get; set; }
        public string? Status { get; set; }
        public string? Reason { get; set; }
        public string? RejectReason { get; set; }
    }
}
