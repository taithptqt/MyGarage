using System;
namespace MyGarage.DTOs.Responses
{
    public class DiagnosisResponseDto
    {
        public long Id { get; set; }
        public long ServiceOrderId { get; set; }
        public long? TechnicianId { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public long? AssignedBayId { get; set; }
        public string? Symptom { get; set; }
        public string? RootCause { get; set; }
        public string? Severity { get; set; }
        public string? Status { get; set; }
        public string? DispatchTag { get; set; }
        public long? AssignedTeamId { get; set; }
        public string? AssignedTeamName { get; set; }
        public string? AssignedTeamSpecialtyTag { get; set; }
        public long? AssignedTeamLeaderId { get; set; }
        public string? AssignedTeamLeaderName { get; set; }
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
        public long? ApprovedByForemanId { get; set; }
        public long? FinalApprovedByManagerId { get; set; }
        public DateTime? FinalApprovedAt { get; set; }
        public string? FinalApprovalNote { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
