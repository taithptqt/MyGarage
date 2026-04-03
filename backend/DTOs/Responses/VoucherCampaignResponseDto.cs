using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Responses
{
    public class VoucherCampaignResponseDto
    {
        public long Id { get; set; }
        public string? CampaignCode { get; set; }
        public string? Name { get; set; }
        public string? Objective { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? BudgetLimit { get; set; }
        public decimal? BudgetUsed { get; set; }
        public decimal? BudgetRemaining { get; set; }
        public decimal? BudgetUsagePercent { get; set; }
        public bool IsNearBudgetLimit { get; set; }
        public bool IsBudgetExceeded { get; set; }
        public string? BudgetAlertLevel { get; set; }
        public string? Status { get; set; }
        public long? OwnerStaffId { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
        public int VoucherCount { get; set; }
        public List<long> VoucherIds { get; set; } = new();
        public List<VoucherCampaignVoucherSummaryDto> Vouchers { get; set; } = new();
    }

    public class VoucherCampaignVoucherSummaryDto
    {
        public long Id { get; set; }
        public string? Code { get; set; }
        public string? VoucherName { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public bool? IsActive { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
