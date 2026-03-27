using System;
using System.Collections.Generic;

namespace MyGarage.DTOs.Requests
{
    public class VoucherCampaignUpdateDto
    {
        public string? CampaignCode { get; set; }
        public string? Name { get; set; }
        public string? Objective { get; set; }
        public string? Description { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal? BudgetLimit { get; set; }
        public decimal? BudgetUsed { get; set; }
        public string? Status { get; set; }
        public long? OwnerStaffId { get; set; }
        public bool? IsActive { get; set; }
        public long? CreatedBy { get; set; }
        public long? UpdatedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public long? DeletedBy { get; set; }
        public List<long>? VoucherIds { get; set; }
        public bool? ReplaceVoucherScope { get; set; }
    }
}
