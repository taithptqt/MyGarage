using System;
namespace MyGarage.DTOs.Requests
{
    public class VehicleModelCreateDto
    {
        public long BrandId { get; set; }
        public string? ModelName { get; set; }
        public string? Segment { get; set; }
        public string? FuelType { get; set; }
        public string? CylinderCapacity { get; set; }
        public int? VehicleClassId { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
