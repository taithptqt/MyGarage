using System;
namespace MyGarage.DTOs.Requests
{
    public class VoucherCreateDto
    {
        public string? Code { get; set; }
        public string? Type { get; set; }
        public decimal? Value { get; set; }
        public decimal? MinOrderValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int? UsageLimit { get; set; }
        public int? UsedCount { get; set; }
        public decimal? MaxDiscountAmount { get; set; }
        public bool? IsStackable { get; set; }
        public TimeOnly? ValidFrom { get; set; }
        public TimeOnly? ValidTo { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
