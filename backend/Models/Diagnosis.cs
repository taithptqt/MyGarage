using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Diagnosis
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public long TechnicianId { get; set; }

    public string? Symptom { get; set; }

    public string? RootCause { get; set; }

    public string? Severity { get; set; }

    public string? Status { get; set; }

    public DateTime? DiagnosedAt { get; set; }

    public long? ApprovedByForemanId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual ICollection<AiTechnicalAssistant> AiTechnicalAssistants { get; set; } = new List<AiTechnicalAssistant>();

    public virtual Staff? ApprovedByForeman { get; set; }

    public virtual ICollection<DiagnoseRecommendation> DiagnoseRecommendations { get; set; } = new List<DiagnoseRecommendation>();

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual Staff Technician { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
