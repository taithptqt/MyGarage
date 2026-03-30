using System;

namespace MyGarage.DTOs.Responses
{
    public class VoucherApprovalRequestResponseDto
    {
        public long Id { get; set; }
        public string? RequestType { get; set; }
        public long? VoucherId { get; set; }
        public long? RedemptionId { get; set; }
        public long? BranchId { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? RetailOrderId { get; set; }
        public long? InvoiceId { get; set; }
        public long RequestedBy { get; set; }
        public long? ApproverId { get; set; }
        public string? Status { get; set; }
        public string? RiskLevel { get; set; }
        public string? Reason { get; set; }
        public string? RejectReason { get; set; }
        public string? RequestPayload { get; set; }
        public decimal? ImpactAmount { get; set; }
        public DateTime? SlaDeadline { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

