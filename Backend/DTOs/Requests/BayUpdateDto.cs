using System;
namespace MyGarage.DTOs.Requests
{
    public class BayUpdateDto
    {
        public long BranchId { get; set; }
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? BayType { get; set; }
        public decimal? MaxTonnage { get; set; }
        public string? OperationalStatus { get; set; }
        public long? CurrentOccupiedVehicleId { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
