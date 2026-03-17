using System;

namespace MyGarage.DTOs.Responses
{
    public class VehicleResponseDto
    {
        public long Id { get; set; }
        public string? LicensePlate { get; set; }
        public string? Vin { get; set; }
        public long ModelId { get; set; }
        public string? Color { get; set; }
        public int? Odo { get; set; }
        public DateTime? OdoLastUpdatedAt { get; set; }
        public int? ManufactureYear { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
