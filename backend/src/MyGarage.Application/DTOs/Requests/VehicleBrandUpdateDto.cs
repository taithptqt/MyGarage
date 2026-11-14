using System;
namespace MyGarage.DTOs.Requests
{
    public class VehicleBrandUpdateDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? LogoUrl { get; set; }
        public string? CountryOfOrigin { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
