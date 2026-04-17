using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Diagnosis
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public string? Symptom { get; set; }

    public string? RootCause { get; set; }

    public string? Severity { get; set; }

    public string? Status { get; set; }

    public string? AssignmentNote { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? SlaDueAt { get; set; }

    public bool IsEscalated { get; set; }

    public string? DispatchTag { get; set; }

    public DateTime? DiagnosedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ICollection<DiagnosisRecommendation> DiagnosisRecommendations { get; set; } = new List<DiagnosisRecommendation>();

    public virtual ICollection<DiagnosisAssignment> DiagnosisAssignments { get; set; } = new List<DiagnosisAssignment>();

    public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();

    public virtual ICollection<TechnicalProposalSnapshot> TechnicalProposalSnapshots { get; set; } = new List<TechnicalProposalSnapshot>();

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
