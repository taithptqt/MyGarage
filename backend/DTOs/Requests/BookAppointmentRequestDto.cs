using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyGarage.Common;

namespace MyGarage.DTOs.Requests
{
    public class BookAppointmentRequestDto
    {
        [Required]
        public long BranchId { get; set; }

        public long? VehicleId { get; set; }

        public long? VehicleModelId { get; set; }

        [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
        public string? LicensePlate { get; set; }

        [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
        public string? Vin { get; set; }

        public string? Color { get; set; }

        [Range(1900, 2100, ErrorMessage = "Năm sản xuất không hợp lệ.")]
        public int? ManufactureYear { get; set; }

        public DateTime? StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public DateTime? CheckInWindowStart { get; set; }

        public DateTime? CheckInWindowEnd { get; set; }

        public string? BookingFlow { get; set; }

        public long? CustomerId { get; set; }

        public string? CustomerFullName { get; set; }

        [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
        public string? CustomerPhone { get; set; }

        public long? CreatedBy { get; set; }

        public string? RequestedServiceIds { get; set; }

        public string? RequestedPackageIds { get; set; }

        public long? InsuranceCompanyId { get; set; }

        [Range(0, 1000000000, ErrorMessage = "Mức miễn thường không hợp lệ.")]
        public decimal? InsuranceDeductibleAmount { get; set; }

        public string? InsuranceDeductibleOption { get; set; }

        public string? InsuranceClaimMode { get; set; }

        public string? InsuranceQuoteState { get; set; }

        public List<string> InsuranceCardImageUrls { get; set; } = new();

        public List<string> InsuranceDamageImageUrls { get; set; } = new();

        [Display(Name = "Ghi chú")]
        public string? CustomerNotes { get; set; }

        public string? SlotHoldToken { get; set; }
    }
}
