using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class StandardService
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public long CategoryId { get; set; }

    public decimal BasePrice { get; set; }

    public string? EngineType { get; set; }

    public int StandardMinutes { get; set; }

    public decimal? TaxRate { get; set; }

    public int? WarrantyMonths { get; set; }

    public int? WarrantyKm { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public decimal? ConsumableCost { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual ServiceCategory Category { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<DiagnosisRecommendation> DiagnosisRecommendations { get; set; } = new List<DiagnosisRecommendation>();

    public virtual ICollection<InvoiceDetail> InvoiceDetails { get; set; } = new List<InvoiceDetail>();

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetails { get; set; } = new List<ServiceOrderDetail>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
