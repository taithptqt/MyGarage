using System;

namespace MyGarage.Application.Accounting;

public sealed record AccountingActorContext(
    bool IsAuthenticated,
    long? UserId,
    long? BranchId,
    string? ClaimRole,
    string? RoleCode,
    string? RoleName);

public sealed record AccountingAccess(long UserId, long? BranchId);

public sealed record AccountingAccessResult(
    bool IsAllowed,
    AccountingAccess? Access = null,
    int DeniedStatusCode = 403,
    string? DeniedMessage = null);

public sealed class PostJournalRequest
{
    public long PostedBy { get; set; }
}

public sealed class FinanceDecisionRequest
{
    public string? Decision { get; set; }
    public long ApproverId { get; set; }
    public string? RejectReason { get; set; }
}

public sealed class PayrollApproveRequest
{
    public long ApprovedBy { get; set; }
}

public sealed class PayrollPaidRequest
{
    public long PaidBy { get; set; }
}

public sealed class SupplierPaymentVoidRequest
{
    public long? ApprovalRequestId { get; set; }
    public long? ApprovedBy { get; set; }
    public string? Reason { get; set; }
}

public sealed class PayrollBatchCalculateRequest
{
    public long? PayrollPeriodId { get; set; }
    public long? PayrollRunId { get; set; }
    public bool OverwriteExisting { get; set; } = true;
    public bool IncludeInactiveStaff { get; set; }
    public bool IssuePayslips { get; set; }
}

public sealed class PayrollRunCalculateRequest
{
    public bool OverwriteExisting { get; set; } = true;
    public bool IncludeInactiveStaff { get; set; }
    public bool IssuePayslips { get; set; }
}

public sealed class CreatePayslipRequest
{
    public long PayrollItemId { get; set; }
    public string? SlipNo { get; set; }
    public string? IssuedAt { get; set; }
    public long? IssuedBy { get; set; }
    public string? FileUrl { get; set; }
}

public sealed class CreatePayrollPeriodRequest
{
    public string? PeriodCode { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
    public string? PayrollDate { get; set; }
    public string? Status { get; set; }
}

public sealed class InitialSalaryRequestDto
{
    public decimal? BaseSalary { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public string? Reason { get; set; }
    public string? Note { get; set; }
}
