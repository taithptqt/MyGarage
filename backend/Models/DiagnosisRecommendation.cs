using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class DiagnosisRecommendation
{
    public long Id { get; set; }

    public long DiagnosisId { get; set; }

    public long? DiagnosisAssignmentId { get; set; }

    public long? ParentRecommendationId { get; set; }

    public string ItemType { get; set; } = null!;

    public long? StandardServiceId { get; set; }

    public long? InventoryItemId { get; set; }

    public decimal Quantity { get; set; }

    public string? RecommendationStatus { get; set; }

    public long? SoftDeletedBy { get; set; }

    public DateTime? SoftDeletedAt { get; set; }

    public string? Urgency { get; set; }

    public string? EvidenceMediaUrls { get; set; }

    public string? TechnicianNotes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Diagnosis Diagnosis { get; set; } = null!;

    public virtual DiagnosisAssignment? DiagnosisAssignment { get; set; }

    public virtual Product? InventoryItem { get; set; }

    public virtual ICollection<DiagnosisRecommendation> InverseParentRecommendation { get; set; } = new List<DiagnosisRecommendation>();

    public virtual DiagnosisRecommendation? ParentRecommendation { get; set; }

    public virtual StandardService? StandardService { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }
}

