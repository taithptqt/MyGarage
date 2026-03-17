using System;
namespace MyGarage.DTOs.Requests
{
    public class StaffTierUpdateDto
    {
        public string? TierName { get; set; }
        public decimal? BaseSalary { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
