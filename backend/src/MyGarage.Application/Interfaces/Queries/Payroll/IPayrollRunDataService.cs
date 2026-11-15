using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Queries;

public sealed record PayrollRunPreviewDetail(
    long StaffId,
    string StaffName,
    decimal BaseSalary,
    decimal AllowanceAmount,
    decimal CommissionAmount,
    decimal OvertimeAmount,
    decimal Bonus,
    decimal Deductions,
    decimal Penalty,
    decimal NetSalary);

public sealed record PayrollRunPreviewResult(
    string Period,
    long BranchId,
    string Status,
    decimal TotalAmount,
    IReadOnlyList<PayrollRunPreviewDetail> Details);

public sealed record PayrollRunCreateLineInput(
    long StaffId,
    decimal? BaseSalary,
    decimal? CommissionAmount,
    decimal? OvertimeAmount,
    decimal? AllowanceAmount,
    decimal? Bonus,
    decimal? BonusAmount,
    decimal? Deductions,
    decimal? Penalty,
    decimal? PenaltyAmount);

public enum PayrollRunCreateStatus
{
    Ok,
    Created,
    BadRequest
}

public sealed record PayrollRunCreateResult(
    PayrollRunCreateStatus Outcome,
    string? ErrorMessage = null,
    PayrollRun? Run = null);

public enum PayrollRunApproveStatus
{
    Ok,
    NotFound,
    Forbidden,
    BadRequest
}

public sealed record PayrollRunApproveResult(
    PayrollRunApproveStatus Outcome,
    string? ErrorMessage = null,
    PayrollRun? Run = null);

public interface IPayrollRunDataService
{
    Task<(IReadOnlyList<PayrollRun> Items, int Total)> GetPagedAsync(
        long? scopedBranchId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PayrollRunPreviewResult> PreviewAsync(
        long targetBranchId,
        string periodCode,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default);

    Task<PayrollRunCreateResult> CreateAsync(
        long targetBranchId,
        string periodCode,
        DateOnly startDate,
        DateOnly endDate,
        long? actorId,
        IReadOnlyList<PayrollRunCreateLineInput>? details,
        CancellationToken ct = default);

    Task<PayrollRunApproveResult> ApproveAsync(
        long id,
        long? scopedBranchId,
        long? actorId,
        CancellationToken ct = default);
}
