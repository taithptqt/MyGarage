using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class PayrollItem
{
    public long Id { get; set; }

    public long PayrollRunId { get; set; }

    public long StaffId { get; set; }

    public decimal BaseSalary { get; set; }

    public decimal AttendanceDeduction { get; set; }

    public decimal OvertimeAmount { get; set; }

    public decimal AllowanceAmount { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal BonusAmount { get; set; }

    public decimal PenaltyAmount { get; set; }

    public decimal AdvanceDeduction { get; set; }

    public decimal LoanDeduction { get; set; }

    public decimal InsuranceEmployeeAmount { get; set; }

    public decimal InsuranceCompanyAmount { get; set; }

    public decimal PitAmount { get; set; }

    public decimal OtherDeduction { get; set; }

    public decimal NetPay { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual PayrollRun PayrollRun { get; set; } = null!;

    public virtual Payslip? Payslip { get; set; }

    public virtual Staff Staff { get; set; } = null!;
}
