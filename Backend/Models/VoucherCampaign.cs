// Mục đích: Entity EF lưu thông tin chiến dịch voucher và theo dõi ngân sách.
using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VoucherCampaign
{
    public long Id { get; set; }

    public string? CampaignCode { get; set; }

    public string Name { get; set; } = null!;

    public string? Objective { get; set; }

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal? BudgetLimit { get; set; }

    public decimal? BudgetUsed { get; set; }

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

    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}

