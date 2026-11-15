using System.Globalization;
using System.Text.Json;
using MyGarage.Application.Common;
using MyGarage.Application.Exceptions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Services;

namespace MyGarage.Application.Accounting;

public sealed class AccountingPayrollCommandService : IAccountingPayrollCommandService
{
    private const string PayrollRunStatusDraft = nameof(PayrollRunStatusCode.DRAFT);
    private const string PayrollRunStatusCalculated = nameof(PayrollRunStatusCode.CALCULATED);
    private const string PayrollRunStatusApproved = nameof(PayrollRunStatusCode.APPROVED);
    private const string PayrollRunStatusPaid = nameof(PayrollRunStatusCode.PAID);
    private const string PayrollItemStatusApproved = nameof(PayrollItemStatusCode.APPROVED);
    private const string PayrollItemStatusPaid = nameof(PayrollItemStatusCode.PAID);
    private const string PayrollPeriodStatusOpen = nameof(PayrollPeriodStatusCode.OPEN);

    private readonly IAccountingTableQueryService _tableQuery;
    private readonly IAccountingTableCommandService _tableCommand;
    private readonly IAccountingSqlQueryService _sqlQuery;
    private readonly IAccountingSqlCommandService _sqlCommand;
    private readonly IAccountingDataService _dataService;
    private readonly IPayrollBatchCalculationService _payrollBatchCalculationService;
    private readonly IAccountingPayrollDataService _payrollDataService;
    private readonly IAccountingSalaryReconciliationService _salaryReconciliationService;

    public AccountingPayrollCommandService(
        IAccountingTableQueryService tableQuery,
        IAccountingTableCommandService tableCommand,
        IAccountingSqlQueryService sqlQuery,
        IAccountingSqlCommandService sqlCommand,
        IAccountingDataService dataService,
        IPayrollBatchCalculationService payrollBatchCalculationService,
        IAccountingPayrollDataService payrollDataService,
        IAccountingSalaryReconciliationService salaryReconciliationService)
    {
        _tableQuery = tableQuery;
        _tableCommand = tableCommand;
        _sqlQuery = sqlQuery;
        _sqlCommand = sqlCommand;
        _dataService = dataService;
        _payrollBatchCalculationService = payrollBatchCalculationService;
        _payrollDataService = payrollDataService;
        _salaryReconciliationService = salaryReconciliationService;
    }

    public async Task<AccountingCommandResult> ApprovePayrollAsync(
        long id,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        var row = await _tableQuery.GetByIdAsync("payroll-runs", id, branchId);
        if (row is null) return new AccountingCommandResult(404);
        var payrollPeriodId = AsLong(row, "payrollPeriodId");
        var runStatus = AsString(row, "status").Trim().ToUpperInvariant();
        if (runStatus is not PayrollRunStatusDraft and not PayrollRunStatusCalculated)
            return new AccountingCommandResult(400, new { message = "Only DRAFT/CALCULATED payroll run can be approved." });

        var lineCount = await _sqlQuery.ScalarAsync<int?>(
            "SELECT COUNT(*) FROM dbo.payroll_items WHERE payroll_run_id=@id;",
            [new QueryParam("@id", id)]);
        if (!lineCount.HasValue || lineCount.Value <= 0)
            return new AccountingCommandResult(400, new { message = "Cannot approve payroll run without payroll items." });

        var reconciliation = await _salaryReconciliationService.BuildPayrollRunCommissionReconciliationAsync(
            id,
            branchId,
            cancellationToken);
        if (!reconciliation.Summary.IsFullyMatched)
        {
            return new AccountingCommandResult(400, new
            {
                message = "Cannot approve payroll run when commission reconciliation is not matched.",
                commissionGap = reconciliation.Summary.TotalGap
            });
        }

        await _sqlCommand.ExecuteAsync(
            "UPDATE dbo.payroll_runs SET status=@status, approved_by=@u, approved_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new QueryParam("@status", PayrollRunStatusApproved),
                new QueryParam("@u", userId),
                new QueryParam("@id", id)
            ]);
        await _sqlCommand.ExecuteAsync(
            $"""
            UPDATE dbo.payroll_items
            SET status=@itemStatusApproved
            WHERE payroll_run_id=@id
              AND UPPER(ISNULL(status, @runStatusDraft)) IN (@runStatusDraft, @runStatusCalculated);
            """,
            [
                new QueryParam("@id", id),
                new QueryParam("@itemStatusApproved", PayrollItemStatusApproved),
                new QueryParam("@runStatusDraft", PayrollRunStatusDraft),
                new QueryParam("@runStatusCalculated", PayrollRunStatusCalculated)
            ]);
        if (payrollPeriodId.HasValue && payrollPeriodId.Value > 0)
            await _payrollDataService.SyncPayrollPeriodStatusAsync(payrollPeriodId.Value);
        return new AccountingCommandResult(200, await _tableQuery.GetByIdAsync("payroll-runs", id, branchId));
    }

    public async Task<AccountingCommandResult> MarkPayrollPaidAsync(long id, long? branchId)
    {
        var row = await _tableQuery.GetByIdAsync("payroll-runs", id, branchId);
        if (row is null) return new AccountingCommandResult(404);
        var payrollPeriodId = AsLong(row, "payrollPeriodId");
        if (!AsString(row, "status").Equals(PayrollRunStatusApproved, StringComparison.OrdinalIgnoreCase) || !AsLong(row, "approvedBy").HasValue)
            return new AccountingCommandResult(400, new { message = "Run must be APPROVED and approvedBy set before PAID." });

        await _sqlCommand.ExecuteAsync(
            "UPDATE dbo.payroll_runs SET status=@status, paid_at=SYSDATETIME(), updated_at=SYSDATETIME() WHERE id=@id;",
            [
                new QueryParam("@id", id),
                new QueryParam("@status", PayrollRunStatusPaid)
            ]);
        await _sqlCommand.ExecuteAsync(
            """
            UPDATE dbo.payroll_items
            SET status=@itemStatusPaid
            WHERE payroll_run_id=@id;
            """,
            [
                new QueryParam("@id", id),
                new QueryParam("@itemStatusPaid", PayrollItemStatusPaid)
            ]);
        if (payrollPeriodId.HasValue && payrollPeriodId.Value > 0)
            await _payrollDataService.SyncPayrollPeriodStatusAsync(payrollPeriodId.Value);
        return new AccountingCommandResult(200, await _tableQuery.GetByIdAsync("payroll-runs", id, branchId));
    }

    public async Task<AccountingCommandResult> CancelPayrollRunAsync(
        long id,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        var (found, errorMessage, payrollPeriodId) = await _dataService.CancelPayrollRunAsync(
            id,
            branchId,
            userId,
            cancellationToken);

        if (!found)
            return AccountingCommandResult.Problem(404, $"Payroll run #{id} not found.");

        if (!string.IsNullOrWhiteSpace(errorMessage))
            return AccountingCommandResult.Problem(400, errorMessage);

        if (payrollPeriodId > 0)
            await _payrollDataService.SyncPayrollPeriodStatusAsync(payrollPeriodId);

        return new AccountingCommandResult(200, await _tableQuery.GetByIdAsync("payroll-runs", id, branchId));
    }

    public Task<AccountingCommandResult> CalculatePayrollRunAsync(
        long id,
        PayrollRunCalculateRequest? request,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        request ??= new PayrollRunCalculateRequest();
        return ExecutePayrollBatchCalculationAsync(
            new PayrollBatchCalculationCommand
            {
                UserId = userId,
                BranchId = branchId,
                PayrollRunId = id,
                OverwriteExisting = request.OverwriteExisting,
                IncludeInactiveStaff = request.IncludeInactiveStaff,
                IssuePayslips = request.IssuePayslips
            },
            cancellationToken);
    }

    public Task<AccountingCommandResult> CalculatePayrollForAllAsync(
        PayrollBatchCalculateRequest? request,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        request ??= new PayrollBatchCalculateRequest();
        return ExecutePayrollBatchCalculationAsync(
            new PayrollBatchCalculationCommand
            {
                UserId = userId,
                BranchId = branchId,
                PayrollPeriodId = request.PayrollPeriodId,
                PayrollRunId = request.PayrollRunId,
                OverwriteExisting = request.OverwriteExisting,
                IncludeInactiveStaff = request.IncludeInactiveStaff,
                IssuePayslips = request.IssuePayslips
            },
            cancellationToken);
    }

    private async Task<AccountingCommandResult> ExecutePayrollBatchCalculationAsync(
        PayrollBatchCalculationCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _payrollBatchCalculationService.CalculatePayrollForAllAsync(command, cancellationToken);
            return new AccountingCommandResult(200, result);
        }
        catch (KeyNotFoundException ex)
        {
            return AccountingCommandResult.Problem(404, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return AccountingCommandResult.Problem(400, ex.Message);
        }
    }

    public async Task<AccountingCommandResult> ReconcilePayrollRunCommissionsAsync(
        long id,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        var (found, status, runNo) = await _dataService.GetPayrollRunStatusAsync(
            id,
            branchId,
            cancellationToken);
        if (!found) return new AccountingCommandResult(404, new { message = $"Payroll run #{id} not found." });

        var runStatus = (status ?? string.Empty).Trim().ToUpperInvariant();
        if (runStatus is not PayrollRunStatusDraft and not PayrollRunStatusCalculated)
        {
            return new AccountingCommandResult(400, new
            {
                message = "Only DRAFT/CALCULATED payroll run can be reconciled.",
                currentStatus = status
            });
        }

        var before = await _salaryReconciliationService.BuildPayrollRunCommissionReconciliationAsync(
            id,
            branchId,
            cancellationToken);

        PayrollBatchCalculationResult calculateResult;
        try
        {
            calculateResult = await _payrollBatchCalculationService.CalculatePayrollForAllAsync(
                new PayrollBatchCalculationCommand
                {
                    UserId = userId,
                    BranchId = branchId,
                    PayrollRunId = id,
                    OverwriteExisting = true,
                    IncludeInactiveStaff = false,
                    IssuePayslips = false
                },
                cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return new AccountingCommandResult(404, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return new AccountingCommandResult(400, new { message = ex.Message });
        }

        var after = await _salaryReconciliationService.BuildPayrollRunCommissionReconciliationAsync(
            id,
            branchId,
            cancellationToken);

        return new AccountingCommandResult(200, new
        {
            message = after.Summary.IsFullyMatched
                ? "Commission reconciliation completed and matched."
                : "Commission reconciliation completed but still has gap.",
            payrollRunId = id,
            runNo,
            beforeSummary = before.Summary,
            afterSummary = after.Summary,
            matched = after.Summary.IsFullyMatched,
            calculateResult = new
            {
                calculateResult.ProcessedStaffCount,
                calculateResult.CalculatedStaffCount,
                calculateResult.Inserted,
                calculateResult.Updated,
                calculateResult.SkippedNoBaseSalary,
                calculateResult.SkippedExisting,
                calculateResult.CommissionTotal,
                calculateResult.ProcessedCommissionTransactions
            },
            reconciliation = after
        });
    }

    public async Task<AccountingCommandResult> IssuePayslipAsync(
        IssuePayslipCommand command,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        if (command.PayrollItemId <= 0)
            return new AccountingCommandResult(400, new { message = "payrollItemId must be a positive number." });

        var payrollItem = await _dataService.GetPayrollItemWithRunAsync(command.PayrollItemId, cancellationToken);
        if (payrollItem is null)
            return new AccountingCommandResult(404, new { message = $"Payroll item #{command.PayrollItemId} not found." });

        if (branchId.HasValue && payrollItem.PayrollRun.BranchId != branchId.Value)
            return new AccountingCommandResult(404, new { message = $"Payroll item #{command.PayrollItemId} not found." });

        var runStatus = (payrollItem.PayrollRun.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (runStatus is not PayrollRunStatusApproved and not PayrollRunStatusPaid)
            return new AccountingCommandResult(400, new { message = "Payslip can only be issued when payroll run is APPROVED or PAID." });

        var existingPayslip = await _dataService.GetPayslipByPayrollItemIdAsync(command.PayrollItemId, cancellationToken);
        if (existingPayslip is not null)
        {
            var existingRow = await _tableQuery.GetByIdAsync("payslips", existingPayslip.Id, branchId);
            return new AccountingCommandResult(200, existingRow is null ? existingPayslip : existingRow);
        }

        var issuedAt = DateTime.UtcNow;
        var issuedAtText = (command.IssuedAt ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(issuedAtText))
        {
            if (!DateTime.TryParse(issuedAtText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out issuedAt))
                return new AccountingCommandResult(400, new { message = "issuedAt is invalid." });
        }

        var slipNo = (command.SlipNo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(slipNo))
            slipNo = $"PS-{payrollItem.PayrollRun.RunNo}-{command.PayrollItemId}-{issuedAt:yyMMddHHmm}";
        if (slipNo.Length > 40)
            return new AccountingCommandResult(400, new { message = "slipNo must be <= 40 characters." });

        var fileUrl = (command.FileUrl ?? string.Empty).Trim();

        var payload = JsonSerializer.SerializeToElement(new
        {
            payrollItemId = command.PayrollItemId,
            slipNo,
            issuedAt,
            issuedBy = userId,
            fileUrl
        });

        try
        {
            var created = await _tableCommand.InsertAsync("payslips", payload, branchId);
            return created is null
                ? new AccountingCommandResult(400, new { message = "Failed to issue payslip." })
                : new AccountingCommandResult(200, created);
        }
        catch (DuplicateKeyException)
        {
            var duplicated = await _dataService.GetPayslipByPayrollItemIdAsync(command.PayrollItemId, cancellationToken);
            if (duplicated is not null)
            {
                var duplicatedRow = await _tableQuery.GetByIdAsync("payslips", duplicated.Id, branchId);
                return new AccountingCommandResult(200, duplicatedRow is null ? duplicated : duplicatedRow);
            }

            return new AccountingCommandResult(409, new { message = "Payslip already exists for this payroll item." });
        }
        catch (DatabaseOperationException ex)
        {
            return new AccountingCommandResult(400, new { message = ex.Message });
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return new AccountingCommandResult(400, new { message = ex.Message });
        }
    }

    public async Task<AccountingCommandResult> CreatePayrollPeriodAsync(
        CreatePayrollPeriodCommand command,
        long? branchId)
    {
        var periodCode = (command.PeriodCode ?? "").Trim();
        if (string.IsNullOrWhiteSpace(periodCode))
            return new AccountingCommandResult(400, new { message = "periodCode is required." });
        if (periodCode.Length > 20)
            return new AccountingCommandResult(400, new { message = "periodCode must be <= 20 characters." });

        if (!DateOnly.TryParse(command.StartDate, out var startDate)
            || !DateOnly.TryParse(command.EndDate, out var endDate)
            || !DateOnly.TryParse(command.PayrollDate, out var payrollDate))
            return new AccountingCommandResult(400, new { message = "startDate/endDate/payrollDate must be valid dates (yyyy-MM-dd)." });

        if (startDate > endDate)
            return new AccountingCommandResult(400, new { message = "startDate must be <= endDate." });
        if (payrollDate < endDate)
            return new AccountingCommandResult(400, new { message = "payrollDate must be >= endDate." });

        try
        {
            var duplicated = await _sqlQuery.ScalarAsync<int?>(
                "SELECT COUNT(*) FROM dbo.payroll_periods WHERE UPPER(period_code)=UPPER(@periodCode);",
                [new QueryParam("@periodCode", periodCode)]);
            if (duplicated.GetValueOrDefault() > 0)
                return new AccountingCommandResult(409, new { message = $"Payroll period code '{periodCode}' already exists." });

            var payload = JsonSerializer.SerializeToElement(new
            {
                periodCode,
                startDate = startDate.ToDateTime(TimeOnly.MinValue),
                endDate = endDate.ToDateTime(TimeOnly.MinValue),
                payrollDate = payrollDate.ToDateTime(TimeOnly.MinValue),
                status = PayrollPeriodStatusOpen
            });

            var created = await _tableCommand.InsertAsync("payroll-periods", payload, branchId);
            return created is null
                ? new AccountingCommandResult(400, new { message = "Failed to create payroll period." })
                : new AccountingCommandResult(200, created);
        }
        catch (DuplicateKeyException)
        {
            return new AccountingCommandResult(409, new { message = $"Payroll period code '{periodCode}' already exists." });
        }
        catch (DatabaseOperationException ex)
        {
            return new AccountingCommandResult(400, new { message = ex.Message });
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            return new AccountingCommandResult(400, new { message = ex.Message });
        }
    }

    public Task<AccountingCommandResult> GetPayrollRunCommissionReconciliationAsync(long id, long? branchId, CancellationToken cancellationToken = default) =>
        _salaryReconciliationService.GetPayrollRunCommissionReconciliationAsync(id, branchId, cancellationToken);

    public Task<AccountingCommandResult> GetStaffBaseSalaryAsync(long staffId, DateTime? asOfDate, long? branchId, CancellationToken cancellationToken = default) =>
        _salaryReconciliationService.GetStaffBaseSalaryAsync(staffId, asOfDate, branchId, cancellationToken);

    public Task<AccountingCommandResult> GetStaffSalaryCoefficientAsync(long staffId, DateTime? asOfDate, long? branchId, CancellationToken cancellationToken = default) =>
        _salaryReconciliationService.GetStaffSalaryCoefficientAsync(staffId, asOfDate, branchId, cancellationToken);

    public Task<AccountingCommandResult> RequestInitialSalaryAsync(long staffId, InitialSalaryCommand command, long userId, long? branchId, CancellationToken cancellationToken = default) =>
        _salaryReconciliationService.RequestInitialSalaryAsync(staffId, command, userId, branchId, cancellationToken);

    private static string AsString(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";

    private static long? AsLong(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt64(v) : null;
}
