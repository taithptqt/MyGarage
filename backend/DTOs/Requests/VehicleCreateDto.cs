using System;

namespace MyGarage.DTOs.Requests
{
    public class VehicleCreateDto
    {
        public string? LicensePlate { get; set; }
        public string? Vin { get; set; }
        public long ModelId { get; set; }
        public string? Color { get; set; }
        public int? Odo { get; set; }
        public int? ManufactureYear { get; set; }
    }
}
