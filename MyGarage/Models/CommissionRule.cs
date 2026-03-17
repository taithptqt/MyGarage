using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class CommissionRule
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public string TargetType { get; set; } = null!;

    public long? TargetId { get; set; }

    public long StaffTierId { get; set; }

    public string CommissionType { get; set; } = null!;

    public decimal CommissionValue { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public long? DeletedBy { get; set; }

    public virtual Branch Branch { get; set; } = null!;

    public virtual Staff? CreatedByNavigation { get; set; }

    public virtual Staff? DeletedByNavigation { get; set; }

    public virtual StaffTier StaffTier { get; set; } = null!;

    public virtual Staff? UpdatedByNavigation { get; set; }
}
