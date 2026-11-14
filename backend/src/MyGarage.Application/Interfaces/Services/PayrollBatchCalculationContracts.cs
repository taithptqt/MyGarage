namespace MyGarage.Services;

public sealed class PayrollBatchCalculationCommand
{
    public long UserId { get; set; }
    public long? BranchId { get; set; }
    public long? PayrollPeriodId { get; set; }
    public long? PayrollRunId { get; set; }
    public bool OverwriteExisting { get; set; } = true;
    public bool IncludeInactiveStaff { get; set; }
    public bool IssuePayslips { get; set; }
}

public sealed class PayrollBatchCalculationResult
{
    public long PayrollPeriodId { get; set; }
    public long PayrollRunId { get; set; }
    public int RunNo { get; set; }
    public bool CreatedRun { get; set; }
    public string PeriodStartDate { get; set; } = "";
    public string PeriodEndDate { get; set; } = "";
    public int ProcessedStaffCount { get; set; }
    public int CalculatedStaffCount { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int SkippedNoBaseSalary { get; set; }
    public int SkippedExisting { get; set; }
    public bool IssuePayslips { get; set; }
    public int PayslipsIssued { get; set; }
    public decimal GrossTotal { get; set; }
    public decimal NetTotal { get; set; }
    public decimal CommissionTotal { get; set; }
    public int ProcessedCommissionTransactions { get; set; }
    public string Message { get; set; } = "";
}
