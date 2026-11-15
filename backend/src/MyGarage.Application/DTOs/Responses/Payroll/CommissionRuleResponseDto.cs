using System;
namespace MyGarage.DTOs.Responses
{
    public class CommissionRuleResponseDto
    {
        public long Id { get; set; }
        public long BranchId { get; set; }
        public string? TargetType { get; set; }
        public long? TargetId { get; set; }
        public long StaffTierId { get; set; }
        public string? CommissionType { get; set; }
        public decimal? CommissionValue { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
    }
}
