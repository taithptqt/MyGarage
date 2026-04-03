using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class SalaryAdvance
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly RequestDate { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public long? DeductFromPayrollPeriodId { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff? ApprovedByNavigation { get; set; }

    public virtual PayrollPeriod? DeductFromPayrollPeriod { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
