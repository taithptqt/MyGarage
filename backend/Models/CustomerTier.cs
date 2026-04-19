using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class CustomerTier
{
    public long Id { get; set; }

    public string TierName { get; set; } = null!;

    public decimal MinSpending { get; set; }

    public decimal DiscountPercent { get; set; }

    public string? Benefits { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<QuotationDiscount> QuotationDiscounts { get; set; } = new List<QuotationDiscount>();

    public virtual ICollection<ServiceOrderDiscount> ServiceOrderDiscounts { get; set; } = new List<ServiceOrderDiscount>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
