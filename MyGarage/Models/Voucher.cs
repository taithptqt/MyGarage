using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class Voucher
{
    public long Id { get; set; }

    public string Code { get; set; } = null!;

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

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<CustomerVoucher> CustomerVouchers { get; set; } = new List<CustomerVoucher>();

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<RetailOrder> RetailOrders { get; set; } = new List<RetailOrder>();

    public virtual ICollection<ServiceOrderDiscount> ServiceOrderDiscounts { get; set; } = new List<ServiceOrderDiscount>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
