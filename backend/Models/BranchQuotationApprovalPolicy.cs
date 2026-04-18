using System;

namespace MyGarage.Models;

public partial class BranchQuotationApprovalPolicy
{
    public long Id { get; set; }

    public long BranchId { get; set; }

    public decimal MaxDiscountPercentWithoutManager { get; set; }

    public decimal HighValueThreshold { get; set; }

    public bool IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? UpdatedBy { get; set; }

    public virtual Branch Branch { get; set; } = null!;
}
