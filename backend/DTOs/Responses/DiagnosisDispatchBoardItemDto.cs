using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class DiagnosisDispatchBoardItemDto
    {
        public long ServiceOrderId { get; set; }
        public string? ServiceOrderCode { get; set; }
        public long? ExecuteBranchId { get; set; }
        public string? ServiceOrderStatus { get; set; }
        public IReadOnlyList<long> DiagnosisIds { get; set; } = Array.Empty<long>();
        public string? QueueState { get; set; }
        public string? QueueColor { get; set; }
        public string? DispatchTag { get; set; }
        public long? TeamId { get; set; }
        public string? TeamName { get; set; }
        public long? PrimaryTeamId { get; set; }
        public string? PrimaryTeamName { get; set; }
        public IReadOnlyList<long> AssignedTeamIds { get; set; } = Array.Empty<long>();
        public IReadOnlyList<string> AssignedTeamNames { get; set; } = Array.Empty<string>();
        public IReadOnlyList<DiagnosisSupportingAssignmentDto> SupportingAssignments { get; set; } = Array.Empty<DiagnosisSupportingAssignmentDto>();
        public long? TechnicianId { get; set; }
        public string? TechnicianName { get; set; }
        public long? AssistantTechnicianId { get; set; }
        public string? AssistantTechnicianName { get; set; }
        public long? AssignedBayId { get; set; }
        public string? AssignedBayCode { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? AssignedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? TechnicianAcceptedAt { get; set; }
        public DateTime? TechnicianCompletedAt { get; set; }
        public int PendingMinutes { get; set; }
        public int AssignedMinutes { get; set; }
        public bool IsSupportRequested { get; set; }
        public DateTime? SupportRequestedAt { get; set; }
        public long? SupportRequestedBy { get; set; }
        public string? SupportRequestNote { get; set; }
        public bool IsCancelPendingApproval { get; set; }
        public string? CancelReason { get; set; }
        public long? TechnicalApprovedByTeamLeaderId { get; set; }
        public DateTime? ApprovedByForemanAt { get; set; }
        public DateTime? TechnicalApprovedAt { get; set; }
        public DateTime? ApprovedByTeamLeaderAt { get; set; }
        public string? TeamLeaderNote { get; set; }
        public string? ForemanNote { get; set; }
    }
}

