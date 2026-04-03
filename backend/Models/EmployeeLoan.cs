using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class EmployeeLoan
{
    public long Id { get; set; }

    public long StaffId { get; set; }

    public decimal LoanAmount { get; set; }

    public decimal OutstandingAmount { get; set; }

    public decimal MonthlyDeduction { get; set; }

    public long? StartPayrollPeriodId { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Staff Staff { get; set; } = null!;

    public virtual PayrollPeriod? StartPayrollPeriod { get; set; }
}
