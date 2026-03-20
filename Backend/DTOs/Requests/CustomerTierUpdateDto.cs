using System;
namespace MyGarage.DTOs.Requests
{
    public class CustomerTierUpdateDto
    {
        public string? TierName { get; set; }
        public decimal? MinSpending { get; set; }
        public decimal? DiscountPercent { get; set; }
        public string? Benefits { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
