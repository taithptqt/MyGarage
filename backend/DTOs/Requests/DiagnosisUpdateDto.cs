using System;
namespace MyGarage.DTOs.Requests
{
    public class DiagnosisUpdateDto
    {
        public long ServiceOrderId { get; set; }
        public long TechnicianId { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public long? AssignedBayId { get; set; }
        public string? Symptom { get; set; }
        public string? RootCause { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
        public string? DispatchTag { get; set; }
        public long? AssignedTeamId { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
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
        public long? AssignedByTeamLeaderId { get; set; }
        public long? ApprovedByForemanId { get; set; }
        public DateTime? ApprovedByForemanAt { get; set; }
        public DateTime? ApprovedByTeamLeaderAt { get; set; }
        public DateTime? FinalApprovedAt { get; set; }
        public string? FinalApprovalNote { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
