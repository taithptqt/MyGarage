using System;
using System.Collections.Generic;

using MyGarage.Interfaces;

namespace MyGarage.Models;

public partial class Appointment : IAuditEntity
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public long CustomerId { get; set; }

    public long? VehicleId { get; set; }

    public long? GroupSessionId { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? RequestedServiceIds { get; set; }

    public string? RequestedPackageIds { get; set; }

    public long? InsuranceCompanyId { get; set; }

    public decimal? InsuranceDeductibleAmount { get; set; }

    public string? InsuranceDeductibleOption { get; set; }

    public string? InsuranceClaimMode { get; set; }

    public string? InsuranceQuoteState { get; set; }

    public string? CustomerNotes { get; set; }

    public bool? IsReminderSent { get; set; }

    public string Status { get; set; } = null!;

    public string? CancelReason { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual GroupSession? GroupSession { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<AppointmentInsuranceAsset> InsuranceAssets { get; set; } = new List<AppointmentInsuranceAsset>();

    public virtual ICollection<AppointmentInsuranceUpload> InsuranceUploads { get; set; } = new List<AppointmentInsuranceUpload>();

    public virtual Customer Customer { get; set; } = null!;

    public virtual InsuranceCompany? InsuranceCompany { get; set; }

    public virtual ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<WorkSlot> WorkSlots { get; set; } = new List<WorkSlot>();

    public virtual Vehicle? Vehicle { get; set; }

    public virtual Staff? UpdatedByNavigation { get; set; }
}
