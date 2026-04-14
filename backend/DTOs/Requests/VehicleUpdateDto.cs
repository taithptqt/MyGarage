using System;
using System.ComponentModel.DataAnnotations;
using MyGarage.Common;

namespace MyGarage.DTOs.Requests
{
    public class VehicleUpdateDto
    {
        [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
        public string? LicensePlate { get; set; }
        [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
        public string? Vin { get; set; }
        public long ModelId { get; set; }
        public string? Color { get; set; }
        public int? Odo { get; set; }
        [Range(1900, 2100, ErrorMessage = "Năm sản xuất không hợp lệ.")]
        public int? ManufactureYear { get; set; }
    }
}
