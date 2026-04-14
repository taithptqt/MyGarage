using System;
namespace MyGarage.DTOs.Responses
{
    public class StaffTierResponseDto
    {
        public long Id { get; set; }
        public string? TierName { get; set; }
        public decimal? BaseSalary { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
