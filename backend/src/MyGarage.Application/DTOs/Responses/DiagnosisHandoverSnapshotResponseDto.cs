using System;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisHandoverSnapshotResponseDto
    {
        public long SnapshotId { get; set; }
        public long ServiceOrderId { get; set; }
        public int RevisionNumber { get; set; }
        public string Stage { get; set; } = "AFTER_DIAGNOSIS_V1";
        public DateTime T1EstimatedHandover { get; set; }
        public int AdditionalRepairMinutes { get; set; }
        public DateTime T2EstimatedHandover { get; set; }
        public string ApprovalState { get; set; } = "PENDING";
        public bool? CustomerApproved { get; set; }
        public string? DecisionChannel { get; set; }
        public string? DecisionNote { get; set; }
        public DateTime? CustomerDecisionAt { get; set; }
        public string? QuoteChannel { get; set; }
        public string? QuoteSummary { get; set; }
        public string? QuoteAttachmentUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
    }
}
