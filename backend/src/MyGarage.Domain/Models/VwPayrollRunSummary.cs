using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VwPayrollRunSummary
{
    public long PayrollRunId { get; set; }

    public long PayrollPeriodId { get; set; }

    public long? BranchId { get; set; }

    public string Status { get; set; } = null!;

    public int? StaffCount { get; set; }

    public decimal? TotalBaseSalary { get; set; }

    public decimal? TotalAllowance { get; set; }

    public decimal? TotalOvertime { get; set; }

    public decimal? TotalCommission { get; set; }

    public decimal? TotalNetPay { get; set; }
}
