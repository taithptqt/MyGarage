using System;
namespace MyGarage.DTOs.Requests
{
    public class ServiceRefundCreateDto
    {
        public long ServiceOrderId { get; set; }
        public decimal? RefundAmount { get; set; }
        public string? Reason { get; set; }
        public string? Status { get; set; }
        public long? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalNote { get; set; }
        public long CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
