using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ServiceOrder
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public long CustomerId { get; set; }

    public long VehicleId { get; set; }

    public long? AppointmentId { get; set; }

    public long? GroupSessionId { get; set; }

    public long OriginBranchId { get; set; }

    public long ExecuteBranchId { get; set; }

    public long? OriginalServiceOrderId { get; set; }

    public string? OrderCategory { get; set; }

    public long? ServiceAdvisorId { get; set; }

    public long? ReceptionistId { get; set; }

    public string? CustomerNotes { get; set; }

    public int? RevisionNumber { get; set; }

    public decimal? SubTotalAmount { get; set; }

    public decimal? TotalDiscount { get; set; }

    public decimal? TaxAmount { get; set; }

    public decimal? FinalAmount { get; set; }

    public decimal? RequiredDepositAmount { get; set; }

    public decimal? TotalDepositedAmount { get; set; }

    public string? DepositStatus { get; set; }

    public decimal? DepositRefundedAmount { get; set; }

    public decimal? DepositForfeitedAmount { get; set; }

    public string Status { get; set; } = null!;

    public string? CancellationReason { get; set; }

    public string? Qcstatus { get; set; }

    public long? QcinspectorId { get; set; }

    public DateTime? QcverifiedAt { get; set; }

    public DateTime? ValidUntil { get; set; }

    public DateTime? CustomerApprovedAt { get; set; }

    public string? OldPartsReturnOption { get; set; }

    public DateTime? CheckInTime { get; set; }

    public string? PaymentStatus { get; set; }

    public DateTime? EstimatedHandover { get; set; }

    public DateTime? PromisedHandoverTime { get; set; }

    public DateTime? ActualHandover { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual BookingGroup? GroupSession { get; set; }

    public virtual ICollection<CommissionTransaction> CommissionTransactions { get; set; } = new List<CommissionTransaction>();

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Customer Customer { get; set; } = null!;

    public virtual ICollection<CustomerFeedback> CustomerFeedbacks { get; set; } = new List<CustomerFeedback>();

    public virtual ICollection<CustomerVoucher> CustomerVouchers { get; set; } = new List<CustomerVoucher>();

    public virtual ICollection<Diagnosis> Diagnoses { get; set; } = new List<Diagnosis>();

    public virtual Branch ExecuteBranch { get; set; } = null!;

    public virtual ICollection<GatePass> GatePasses { get; set; } = new List<GatePass>();

    public virtual ICollection<ServiceOrder> InverseOriginalServiceOrder { get; set; } = new List<ServiceOrder>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<MaterialRequest> MaterialRequests { get; set; } = new List<MaterialRequest>();

    public virtual ICollection<MaterialReturn> MaterialReturns { get; set; } = new List<MaterialReturn>();

    public virtual Branch OriginBranch { get; set; } = null!;

    public virtual ServiceOrder? OriginalServiceOrder { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<QcInspection> QcInspections { get; set; } = new List<QcInspection>();

    public virtual Staff? Qcinspector { get; set; }

    public virtual Staff? Receptionist { get; set; }

    public virtual Staff? ServiceAdvisor { get; set; }

    public virtual ICollection<ServiceOrderDetail> ServiceOrderDetails { get; set; } = new List<ServiceOrderDetail>();

    public virtual ICollection<ServiceOrderDiscount> ServiceOrderDiscounts { get; set; } = new List<ServiceOrderDiscount>();

    public virtual ICollection<ServiceOrderSnapshot> ServiceOrderSnapshots { get; set; } = new List<ServiceOrderSnapshot>();

    public virtual ICollection<ServiceRefund> ServiceRefunds { get; set; } = new List<ServiceRefund>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual ICollection<VehicleConditionLog> VehicleConditionLogs { get; set; } = new List<VehicleConditionLog>();

    public virtual ICollection<VoucherApprovalRequest> VoucherApprovalRequests { get; set; } = new List<VoucherApprovalRequest>();

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();
}
