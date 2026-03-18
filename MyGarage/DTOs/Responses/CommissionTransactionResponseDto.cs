using System;
namespace MyGarage.DTOs.Responses
{
    public class CommissionTransactionResponseDto
    {
        public long Id { get; set; }
        public long StaffId { get; set; }
        public long? ServiceOrderId { get; set; }
        public long? ServiceStaffAssignmentId { get; set; }
        public long? RetailOrderId { get; set; }
        public long? CommissionRuleId { get; set; }
        public string? SourceType { get; set; }
        public decimal? BaseAmount { get; set; }
        public string? AppliedCommissionType { get; set; }
        public decimal? AppliedCommissionValue { get; set; }
        public decimal? AppliedSplitRatio { get; set; }
        public decimal? CommissionAmount { get; set; }
        public string? LogType { get; set; }
        public long? OriginalCommissionId { get; set; }
        public long? ServiceRefundId { get; set; }
        public long? RetailReturnId { get; set; }
        public string? Status { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public long? ApprovedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
