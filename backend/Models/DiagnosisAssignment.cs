using System;
using System.Collections.Generic;


namespace MyGarage.Models;

public partial class DiagnosisAssignment
{
    public long Id { get; set; }

    public long DiagnosisId { get; set; }

    public long? AssignedTeamId { get; set; }

    public long? TechnicianId { get; set; }

    public long? AssistantTechnicianId { get; set; }

    public long? AssignedBayId { get; set; }

    public string? Status { get; set; }

    public DateTime? AssignedAt { get; set; }

    public DateTime? TechnicianAcceptedAt { get; set; }

    public DateTime? TechnicianCompletedAt { get; set; }

    public string? CancellationStatus { get; set; }

    public string? CancellationReason { get; set; }

    public long? CancellationRequestedBy { get; set; }

    public DateTime? CancellationRequestedAt { get; set; }

    public long? CancellationDecisionBy { get; set; }

    public DateTime? CancellationDecisionAt { get; set; }

    public string? CancellationDecisionNote { get; set; }

    public long? SupportRequestedBy { get; set; }

    public DateTime? SupportRequestedAt { get; set; }

    public string? SupportRequestNote { get; set; }

    public string? TeamLeaderApprovalStatus { get; set; }

    public string? FinalApprovalStatus { get; set; }

    public string? ForemanRejectReason { get; set; }

    public long? ApprovedByForemanId { get; set; }

    public DateTime? ApprovedByForemanAt { get; set; }

    public DateTime? ApprovedByTeamLeaderAt { get; set; }

    public long? ApprovedByTeamLeaderId { get; set; }

    public string? TeamLeaderNote { get; set; }

    public string? ForemanNote { get; set; }

    public bool RequiresInsuranceProof { get; set; }

    public string? ProofImageUrl { get; set; }

    public string EventType { get; set; } = null!;

    public string? OldStatus { get; set; }

    public string? Note { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Diagnosis Diagnosis { get; set; } = null!;

    public virtual ICollection<DiagnoseRecommendation> DiagnoseRecommendations { get; set; } = new List<DiagnoseRecommendation>();
}




