using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyGarage.Models;

public partial class Diagnosis
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    [NotMapped]
    public long? TechnicianId { get; set; }

    [NotMapped]
    public long? AssistantTechnicianId { get; set; }

    [NotMapped]
    public long? AssignedBayId { get; set; }

    public string? Symptom { get; set; }

    public string? RootCause { get; set; }

    public string? Severity { get; set; }

    public string? Status { get; set; }

    public string? DispatchTag { get; set; }

    [NotMapped]
    public long? AssignedTeamId { get; set; }

    [NotMapped]
    public DateTime? AssignedAt { get; set; }

    [NotMapped]
    public DateTime? TechnicianAcceptedAt { get; set; }

    [NotMapped]
    public DateTime? TechnicianCompletedAt { get; set; }

    public DateTime? DiagnosedAt { get; set; }

    [NotMapped]
    public DateTime? SupportRequestedAt { get; set; }

    [NotMapped]
    public long? SupportRequestedBy { get; set; }

    [NotMapped]
    public string? SupportRequestNote { get; set; }

    [NotMapped]
    public string? CancellationStatus { get; set; }

    [NotMapped]
    public string? CancellationReason { get; set; }

    [NotMapped]
    public DateTime? CancellationRequestedAt { get; set; }

    [NotMapped]
    public long? CancellationRequestedBy { get; set; }

    [NotMapped]
    public DateTime? CancellationDecisionAt { get; set; }

    [NotMapped]
    public long? CancellationDecisionBy { get; set; }

    [NotMapped]
    public string? CancellationDecisionNote { get; set; }

    [NotMapped]
    public long? ApprovedByForemanId { get; set; }

    [NotMapped]
    public DateTime? ApprovedByForemanAt { get; set; }

    [NotMapped]
    public DateTime? ApprovedByTeamLeaderAt { get; set; }

    [NotMapped]
    public string? TeamLeaderNote { get; set; }

    [NotMapped]
    public string? ForemanNote { get; set; }

    [NotMapped]
    public string? DispatchCollaborationMeta { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ICollection<DiagnoseRecommendation> DiagnoseRecommendations { get; set; } = new List<DiagnoseRecommendation>();

    public virtual ICollection<DiagnosisAssignment> DiagnosisAssignments { get; set; } = new List<DiagnosisAssignment>();

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}

