using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisResponseDto
    {
        public long Id { get; set; }
        public long? DiagnosisAssignmentId { get; set; }
        public long ServiceOrderId { get; set; }
        public long? TechnicianId { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public long? AssignedBayId { get; set; }
        public string? Symptom { get; set; }
        public string? RootCause { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
        public string? AssignmentNote { get; set; }
        public DateTime? SubmittedAt { get; set; }
        public DateTime? SlaDueAt { get; set; }
        public bool IsEscalated { get; set; }
        public string? TeamLeaderApprovalStatus { get; set; }
        public string? FinalApprovalStatus { get; set; }
        public int TeamLeaderApprovalPendingMinutes { get; set; }
        public bool IsTeamLeaderApprovalWarning { get; set; }
        public bool IsTeamLeaderApprovalEscalated { get; set; }
        public string? ForemanRejectReason { get; set; }
        public string? ApprovalStatus { get; set; }
        public string? DispatchTag { get; set; }
        public long? AssignedTeamId { get; set; }
        public string? AssignedTeamName { get; set; }
        public string? AssignedTeamSpecialtyTag { get; set; }
        public long? AssignedTeamLeaderId { get; set; }
        public string? AssignedTeamLeaderName { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? TechnicianAcceptedAt { get; set; }
        public DateTime? TechnicianCompletedAt { get; set; }
        public DateTime? DiagnosedAt { get; set; }
        public DateTime? SupportRequestedAt { get; set; }
        public long? SupportRequestedBy { get; set; }
        public string? SupportRequestNote { get; set; }
        public string? CancellationStatus { get; set; }
        public string? CancellationReason { get; set; }
        public DateTime? CancellationRequestedAt { get; set; }
        public long? CancellationRequestedBy { get; set; }
        public DateTime? CancellationDecisionAt { get; set; }
        public long? CancellationDecisionBy { get; set; }
        public string? CancellationDecisionNote { get; set; }
        public long? ApprovedByForemanId { get; set; }
        public string? ApprovedByForemanName { get; set; }
        public DateTime? ApprovedByForemanAt { get; set; }
        public long? ApprovedByTeamLeaderId { get; set; }
        public string? ApprovedByTeamLeaderName { get; set; }
        public DateTime? ApprovedByTeamLeaderAt { get; set; }
        public string? TeamLeaderNote { get; set; }
        public string? ForemanNote { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public List<DiagnosisAssignmentVersionDto> AssignmentVersions { get; set; } = new();
    }
}
