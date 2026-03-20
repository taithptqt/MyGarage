using System;
namespace MyGarage.DTOs.Responses
{
    public class VehicleModelResponseDto
    {
        public long Id { get; set; }
        public long BrandId { get; set; }
        public string? ModelName { get; set; }
        public string? Segment { get; set; }
        public string? FuelType { get; set; }
        public string? CylinderCapacity { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
