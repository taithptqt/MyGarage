using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyGarage.Common;

namespace MyGarage.DTOs.Requests
{
    public class WorkshopServiceOrderCreateDto
    {
        public string? Code { get; set; }
        public long? CustomerId { get; set; }
        public long? VehicleId { get; set; }
        public long? GroupSessionId { get; set; }
        public long? OriginBranchId { get; set; }
        public long? ExecuteBranchId { get; set; }
        public string? OrderCategory { get; set; }
        public long? ServiceAdvisorId { get; set; }
        public string? CustomerNotes { get; set; }
        public decimal? RequiredDepositAmount { get; set; }
        public string? OldPartsReturnOption { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? EstimatedHandover { get; set; }
        public DateTime? PromisedHandoverTime { get; set; }
        public string? Status { get; set; }

        public bool IsQuickOrder { get; set; }
        public bool IsNewVehicle { get; set; }

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

        public List<long> ServiceIds { get; set; } = new();
        public List<long> PackageIds { get; set; } = new();
    }
}
