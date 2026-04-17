using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ServiceOrderDetail
{
    public long Id { get; set; }

    public long ServiceOrderId { get; set; }

    public long? QuotationDetailId { get; set; }

    public long? StandardServiceId { get; set; }

    public long? TechnicianId { get; set; }

    public long? OriginalOrderDetailId { get; set; }

    public long? SupplierId { get; set; }

    public string ItemType { get; set; } = null!;

    public long? ItemId { get; set; }

    public string ItemName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal? BuyoutCost { get; set; }

    public bool? IsWarrantyItem { get; set; }

    public bool? IsCustomerBroughtPart { get; set; }

    public decimal Quantity { get; set; }

    public int? EstimatedMinutes { get; set; }

    public decimal? IssuedQuantity { get; set; }

    public decimal? CostPriceAtTime { get; set; }

    public decimal UnitPriceAtTime { get; set; }

    public string Unit { get; set; } = null!;

    public decimal SubTotal { get; set; }

    public decimal? DiscountValue { get; set; }

    public decimal? DiscountAllocatedAmount { get; set; }

    public decimal TaxRate { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string PayerType { get; set; } = null!;

    public long? InsuranceCompanyId { get; set; }

    public decimal? InsuranceRequestedAmount { get; set; }

    public decimal? InsuranceApprovedAmount { get; set; }

    public decimal? CustomerCoPayAmount { get; set; }

    public string? InsuranceStatus { get; set; }

    public decimal? GarageAbsorbedAmount { get; set; }

    public long? AbsorbedApprovedBy { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public bool? IsReturnOldPart { get; set; }

    public string ServiceSource { get; set; } = null!;

    public bool IsSupplementary { get; set; }

    public string? SupplementaryReasonCode { get; set; }

    public long? SourceQuotationId { get; set; }

    public long? SupplementaryRequestedByStaffId { get; set; }

    public DateTime? SupplementaryRequestedAt { get; set; }

    public DateTime? CustomerApprovedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Staff? AbsorbedApprovedByNavigation { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual InsuranceCompany? InsuranceCompany { get; set; }

    public virtual ICollection<ServiceOrderDetail> InverseOriginalOrderDetail { get; set; } = new List<ServiceOrderDetail>();

    public virtual ICollection<MaterialRequestDetail> MaterialRequestDetails { get; set; } = new List<MaterialRequestDetail>();

    public virtual ServiceOrderDetail? OriginalOrderDetail { get; set; }

    public virtual ICollection<QcInspection> QcInspections { get; set; } = new List<QcInspection>();

    public virtual ServiceOrder ServiceOrder { get; set; } = null!;

    public virtual QuotationDetail? QuotationDetail { get; set; }

    public virtual Quotation? SourceQuotation { get; set; }

    public virtual ICollection<ServiceStaffAssignment> ServiceStaffAssignments { get; set; } = new List<ServiceStaffAssignment>();

    public virtual StandardService? StandardService { get; set; }

    public virtual Supplier? Supplier { get; set; }

    public virtual Staff? Technician { get; set; }

    public virtual Staff? SupplementaryRequestedByStaff { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }
}

