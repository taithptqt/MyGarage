using System.ComponentModel.DataAnnotations;
using MyGarage.Common;

namespace MyGarage.DTOs.Requests;

public sealed class CustomerPortalVehicleUpsertDto
{
    [Required]
    [MaxLength(32)]
    [RegularExpression(ValidationPatterns.VietnamLicensePlate, ErrorMessage = ValidationMessages.VietnamLicensePlate)]
    public string LicensePlate { get; set; } = "";

    [MaxLength(17)]
    [RegularExpression(ValidationPatterns.Vin17, ErrorMessage = ValidationMessages.Vin17)]
    public string? Vin { get; set; }

    [Required]
    public long ModelId { get; set; }

    [MaxLength(64)]
    public string? Color { get; set; }

    [Range(1900, 2100, ErrorMessage = "Năm sản xuất không hợp lệ.")]
    public int? ManufactureYear { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Số ODO không hợp lệ.")]
    public int? Odo { get; set; }
}
