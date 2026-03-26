using MyGarage.Interfaces;

using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class DiagnoseRecommendation : IAuditEntity
{
    public long Id { get; set; }

    public long DiagnoseId { get; set; }

    public long? ParentRecommendationId { get; set; }

    public string ItemType { get; set; } = null!;

    public long? StandardServiceId { get; set; }

    public long? InventoryItemId { get; set; }

    public decimal Quantity { get; set; }

    public string? Urgency { get; set; }

    public string? EvidenceMediaUrls { get; set; }

    public string? TechnicianNotes { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Diagnosis Diagnose { get; set; } = null!;

    public virtual Product? InventoryItem { get; set; }

    public virtual ICollection<DiagnoseRecommendation> InverseParentRecommendation { get; set; } = new List<DiagnoseRecommendation>();

    public virtual DiagnoseRecommendation? ParentRecommendation { get; set; }

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetails { get; set; } = new List<ServiceOrderDetail>();

    public virtual StandardService? StandardService { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }
}
