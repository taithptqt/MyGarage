using System;
namespace MyGarage.DTOs.Responses
{
    public class VoucherResponseDto
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? VoucherName { get; set; }
        public string? Description { get; set; }
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
        public string? Status { get; set; }
        public long? CampaignId { get; set; }
        public int? MaxUsesPerCustomer { get; set; }
        public int? MaxUsesPerVehicle { get; set; }
        public int? IssuedCount { get; set; }
        public int? RedeemedCount { get; set; }
        public decimal? BudgetLimit { get; set; }
        public decimal? BudgetUsed { get; set; }
        public bool? FirstUseOnly { get; set; }
        public bool? ExcludeDiscountedItems { get; set; }
        public string? AllowedPaymentMethods { get; set; }
        public string? AllowedChannels { get; set; }
        public string? ValidDaysOfWeek { get; set; }
        public string? Timezone { get; set; }
        public bool? ApprovalRequired { get; set; }
        public decimal? ApprovalThresholdAmount { get; set; }
        public byte[]? Version { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
