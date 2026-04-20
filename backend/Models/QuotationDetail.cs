using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class QuotationDetail
{
    public long Id { get; set; }

    public long QuotationId { get; set; }

    public long ServiceOrderId { get; set; }

    public long? SourceQuotationDetailId { get; set; }

    public string ItemType { get; set; } = null!;

    public long? ItemId { get; set; }

    public string ItemName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPriceAtTime { get; set; }

    public decimal? CostPriceAtTime { get; set; }

    public decimal SubTotal { get; set; }

    [Obsolete("Sử dụng bảng QuotationDiscounts thay thế")]
    public decimal? DiscountPercentage { get; set; }

    [Obsolete("Sử dụng bảng QuotationDiscounts thay thế")]
    public decimal DiscountValue { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string PayerType { get; set; } = null!;

    public long? InsuranceCompanyId { get; set; }

    public decimal? InsuranceCoveredAmount { get; set; }

    public decimal? CustomerCoveredAmount { get; set; }

    public string ApprovalStatus { get; set; } = null!;

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectReason { get; set; }

    public string Status { get; set; } = null!;

    public string? CancelReason { get; set; }

    public long? CanceledBy { get; set; }

    public bool IsBlocking { get; set; }

    public bool IsMandatory { get; set; }

    public int? EstimatedMinutes { get; set; }

    public bool IsReturnOldPart { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Quotation Quotation { get; set; } = null!;

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual InsuranceCompany? InsuranceCompany { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Staff? CanceledByNavigation { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetails { get; set; } = new List<ServiceOrderDetail>();

    public virtual ICollection<MandatoryOverrideRequest> MandatoryOverrideRequests { get; set; } = new List<MandatoryOverrideRequest>();
}
