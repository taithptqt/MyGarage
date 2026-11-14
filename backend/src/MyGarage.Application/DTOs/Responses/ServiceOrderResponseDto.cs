using System;
using System.Collections.Generic;
namespace MyGarage.DTOs.Responses
{
    public class ServiceOrderResponseDto
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public long CustomerId { get; set; }
        public long VehicleId { get; set; }
        public string? VehicleLicensePlate { get; set; }
        public long? VehicleBrandId { get; set; }
        public string? VehicleBrandName { get; set; }
        public long? VehicleModelId { get; set; }
        public string? VehicleModelName { get; set; }
        public string? GuestName { get; set; }
        public string? GuestPhone { get; set; }
        public string? GuestVehicleInfo { get; set; }
        public long? AppointmentId { get; set; }
        public long? GroupSessionId { get; set; }
        public long OriginBranchId { get; set; }
        public long ExecuteBranchId { get; set; }
        public long? OriginalServiceOrderId { get; set; }
        public string? OrderCategory { get; set; }
        public long? ServiceAdvisorId { get; set; }
        public long? ReceptionistId { get; set; }
        public long? InsuranceCompanyId { get; set; }
        public decimal? InsuranceDeductibleAmount { get; set; }
        public string? InsuranceDeductibleOption { get; set; }
        public string? InsuranceClaimMode { get; set; }
        public string? InsuranceQuoteState { get; set; }
        public List<string> InsuranceCardImageUrls { get; set; } = new();
        public List<string> InsuranceDamageImageUrls { get; set; } = new();
        public string? CustomerNotes { get; set; }
        public string? RequestedPackageIds { get; set; }
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
        public string? Status { get; set; }
        public string? CancellationReason { get; set; }
        public string? Qcstatus { get; set; }
        public long? QcinspectorId { get; set; }
        public DateTime? QcverifiedAt { get; set; }
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
    }
}
