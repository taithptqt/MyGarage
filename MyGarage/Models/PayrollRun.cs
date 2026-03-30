using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class PayrollRun
{
    public long Id { get; set; }

    public long PayrollPeriodId { get; set; }

    public long? BranchId { get; set; }

    public int RunNo { get; set; }

    public string Status { get; set; } = null!;

    public long? CalculatedBy { get; set; }

    public DateTime? CalculatedAt { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual Branch? Branch { get; set; }

    public virtual Staff? CalculatedByNavigation { get; set; }

    public virtual ICollection<PayrollItem> PayrollItems { get; set; } = new List<PayrollItem>();

    public virtual PayrollPeriod PayrollPeriod { get; set; } = null!;
}
