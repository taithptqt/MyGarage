// Mục đích: Entity EF lưu cấu hình voucher, giới hạn, vòng đời và các quan hệ.
using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Voucher
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

    public string? VoucherName { get; set; }

    public string? Description { get; set; }

    public string Type { get; set; } = null!;

    public decimal Value { get; set; }

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

    public virtual VoucherCampaign? Campaign { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<CustomerVoucher> CustomerVouchers { get; set; } = new List<CustomerVoucher>();

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<RetailOrder> RetailOrders { get; set; } = new List<RetailOrder>();

    public virtual ICollection<ServiceOrderDiscount> ServiceOrderDiscounts { get; set; } = new List<ServiceOrderDiscount>();

    public virtual Staff? UpdatedByNavigation { get; set; }

    public virtual ICollection<VoucherApprovalRequest> VoucherApprovalRequests { get; set; } = new List<VoucherApprovalRequest>();

    public virtual ICollection<VoucherAssignment> VoucherAssignments { get; set; } = new List<VoucherAssignment>();

    public virtual ICollection<VoucherAuditLog> VoucherAuditLogs { get; set; } = new List<VoucherAuditLog>();

    public virtual ICollection<VoucherCodeBatch> VoucherCodeBatches { get; set; } = new List<VoucherCodeBatch>();

    public virtual ICollection<VoucherConflictRule> VoucherConflictRules { get; set; } = new List<VoucherConflictRule>();

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();

    public virtual ICollection<VoucherUsageRule> VoucherUsageRules { get; set; } = new List<VoucherUsageRule>();
}

