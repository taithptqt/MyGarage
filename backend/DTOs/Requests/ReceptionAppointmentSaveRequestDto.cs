using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyGarage.Common;

namespace MyGarage.DTOs.Requests
{
    public class ReceptionAppointmentSaveRequestDto
    {
        public long BranchId { get; set; }
        public bool IsQuickOrder { get; set; }
        public bool IsNewVehicle { get; set; }
        public long? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
        public string? CustomerPhone { get; set; }
        public string? GuestName { get; set; }
        [RegularExpression(ValidationPatterns.VietnamPhone10, ErrorMessage = ValidationMessages.VietnamPhone10)]
        public string? GuestPhone { get; set; }
        [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
        public string? GuestVehiclePlate { get; set; }
        public long? GuestVehicleModelId { get; set; }
        public string? GuestVehicleColor { get; set; }
        [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
        public string? GuestVehicleVin { get; set; }
        [Range(1900, 2100, ErrorMessage = "Năm sản xuất không hợp lệ.")]
        public int? GuestVehicleYear { get; set; }
        [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
        public string? VehicleLicensePlate { get; set; }
        public long? VehicleModelId { get; set; }
        public string? VehicleColor { get; set; }
        [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
        public string? VehicleVin { get; set; }
        [Range(1900, 2100, ErrorMessage = "Năm sản xuất không hợp lệ.")]
        public int? VehicleYear { get; set; }
        public long? InsuranceCompanyId { get; set; }
        [Range(0, 1000000000, ErrorMessage = "Mức miễn thường không hợp lệ.")]
        public decimal? InsuranceDeductibleAmount { get; set; }
        public string? InsuranceDeductibleOption { get; set; }
        public string? InsuranceClaimMode { get; set; }
        public string? InsuranceQuoteState { get; set; }
        public List<string> InsuranceCardImageUrls { get; set; } = new();
        public List<string> InsuranceDamageImageUrls { get; set; } = new();
        public string? CustomerNotes { get; set; }
        public long? ReplaceAppointmentId { get; set; }
        public List<ReceptionAppointmentVehiclePlanItemDto> VehiclePlans { get; set; } = new();
    }

    public class ReceptionAppointmentVehiclePlanItemDto
    {
        public string? VehicleKey { get; set; }
        public long? VehicleId { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? CheckInWindowStart { get; set; }
        public DateTime? CheckInWindowEnd { get; set; }
        public string? BookingFlow { get; set; }
        public int? TotalMinutes { get; set; }
        public string? RequestedServiceIds { get; set; }
        public string? RequestedPackageIds { get; set; }
        public string? CustomerNotes { get; set; }
        public string? SlotHoldToken { get; set; }
        [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
        public string? LicensePlate { get; set; }
        public long? VehicleModelId { get; set; }
        [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
        public string? Vin { get; set; }
        public string? Color { get; set; }
        public long? InsuranceCompanyId { get; set; }
        [Range(0, 1000000000, ErrorMessage = "Mức miễn thường không hợp lệ.")]
        public decimal? InsuranceDeductibleAmount { get; set; }
        public string? InsuranceDeductibleOption { get; set; }
        public string? InsuranceClaimMode { get; set; }
        public string? InsuranceQuoteState { get; set; }
        public List<string> InsuranceCardImageUrls { get; set; } = new();
        public List<string> InsuranceDamageImageUrls { get; set; } = new();
    }
}
