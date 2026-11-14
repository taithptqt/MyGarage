using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class AppointmentPartShortageItemResponseDto
    {
        public long ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal RequiredQuantity { get; set; }
        public decimal AvailableQuantity { get; set; }
        public string? Unit { get; set; }
    }

    public class AppointmentPartShortageServiceResponseDto
    {
        public long ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public List<AppointmentPartShortageItemResponseDto> MissingParts { get; set; } = new();
    }

    public class AppointmentDetailResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public long CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleLicensePlate { get; set; }
        public string? VehicleModelName { get; set; }
        public long? GroupSessionId { get; set; }
        public string? GroupSessionCode { get; set; }
        public string? GroupSessionStatus { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? RequestedServiceIds { get; set; }
        public string? RequestedPackageIds { get; set; }
        public long? InsuranceCompanyId { get; set; }
        public string? InsuranceCompanyName { get; set; }
        public decimal? InsuranceDeductibleAmount { get; set; }
        public string? InsuranceDeductibleOption { get; set; }
        public string? InsuranceClaimMode { get; set; }
        public string? InsuranceQuoteState { get; set; }
        public List<string> InsuranceCardImageUrls { get; set; } = new();
        public List<string> InsuranceDamageImageUrls { get; set; } = new();
        public List<string> ServiceNames { get; set; } = new();
        public List<string> PackageNames { get; set; } = new();
        public string? CustomerNotes { get; set; }
        public bool RequiresDispatchReview { get; set; }
        public string? DispatchReviewNote { get; set; }
        public bool RequiresPartsFollowUp { get; set; }
        public string? PartsFollowUpNote { get; set; }
        public List<AppointmentPartShortageServiceResponseDto> PartsFollowUpServices { get; set; } = new();
        public bool? IsReminderSent { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? CancelReason { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
