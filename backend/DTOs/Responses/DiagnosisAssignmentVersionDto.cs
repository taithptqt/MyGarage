using System;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisAssignmentVersionDto
    {
        public long DiagnosisAssignmentId { get; set; }
        public string? EventType { get; set; }
        public string? Status { get; set; }
        public string? TeamLeaderApprovalStatus { get; set; }
        public string? FinalApprovalStatus { get; set; }
        public string? ForemanRejectReason { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? TechnicianAcceptedAt { get; set; }
        public DateTime? TechnicianCompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public string? Note { get; set; }
        public bool IsCurrent { get; set; }
    }
}
