using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class StaffTier
{
    public long Id { get; set; }

    public string TierName { get; set; } = null!;

    public decimal? BaseSalary { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual ICollection<CommissionRule> CommissionRules { get; set; } = new List<CommissionRule>();

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual ICollection<Staff> Staff { get; set; } = new List<Staff>();

    public virtual Staff? UpdatedByNavigation { get; set; }
}
