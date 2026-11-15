using MyGarage.Application.Common;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Application.Accounting;

public interface IAccountingPayrollDataService
{
    Task SyncPayrollPeriodStatusAsync(long payrollPeriodId);
    Task<PayrollRunResolution> ResolveOrCreateDraftPayrollRunAsync(long payrollPeriodId, long? branchId, long calculatedByUserId);
    Task<Dictionary<long, decimal>> LoadProfileBaseSalaryMapAsync(IReadOnlyCollection<long> staffIds, long? branchId, DateOnly asOfDate);
    Task<Dictionary<long, ContractCompensationSnapshot>> LoadContractCompensationMapAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate);
    Task<Dictionary<long, decimal>> LoadStaffTierBaseSalaryMapAsync(IReadOnlyCollection<long> staffIds, CancellationToken cancellationToken = default);
    Task<Dictionary<long, decimal>> LoadOvertimeAmountMapAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate);
    Task<Dictionary<long, decimal>> LoadAttendanceDeductionMapAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate);
    Task<Dictionary<long, decimal>> LoadSalaryAdvanceDeductionMapAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId);
    Task<Dictionary<long, decimal>> LoadLoanDeductionMapAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId);
    Task<Dictionary<long, int>> LoadDependentCountMapAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate);
}

public sealed class AccountingPayrollDataService : IAccountingPayrollDataService
{
    private const string JournalStatusPosted = nameof(JournalEntryStatusCode.POSTED);
    private const string PayrollRunStatusDraft = nameof(PayrollRunStatusCode.DRAFT);
    private const string PayrollRunStatusCalculated = nameof(PayrollRunStatusCode.CALCULATED);
    private const string PayrollRunStatusApproved = nameof(PayrollRunStatusCode.APPROVED);
    private const string PayrollRunStatusPaid = nameof(PayrollRunStatusCode.PAID);
    private const string PayrollRunStatusLocked = nameof(PayrollRunStatusCode.LOCKED);
    private const string PayrollPeriodStatusOpen = nameof(PayrollPeriodStatusCode.OPEN);
    private const string PayrollPeriodStatusApproved = nameof(PayrollPeriodStatusCode.APPROVED);
    private const string PayrollPeriodStatusPaid = nameof(PayrollPeriodStatusCode.PAID);
    private const string PayrollPeriodStatusLocked = nameof(PayrollPeriodStatusCode.LOCKED);
    private const string StaffSalaryProfileStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);
    private const string StaffContractStatusActive = nameof(StaffContractStatusCode.ACTIVE);
    private const string EmployeeLoanStatusActive = nameof(EmployeeLoanStatusCode.ACTIVE);
    private const string SalaryAdvanceStatusApproved = nameof(SalaryAdvanceStatusCode.APPROVED);
    private const string SalaryAdvanceStatusDeducting = nameof(SalaryAdvanceStatusCode.DEDUCTING);
    private const string AdjustmentApprovedStatus = nameof(AttendanceAdjustmentStatusCode.APPROVED);
    private const string AdjustmentPaidStatus = nameof(OvertimeEntryStatusCode.PAID);
    private const string PaymentCompletedStatus = nameof(PaymentStatusCode.COMPLETED);

    private readonly IAccountingSchemaService _schema;
    private readonly IAccountingTableQueryService _tableQuery;
    private readonly IAccountingSqlQueryService _sqlQuery;
    private readonly IAccountingSqlCommandService _sqlCommand;
    private readonly IAccountingDataService _dataService;

    public AccountingPayrollDataService(
        IAccountingSchemaService schema,
        IAccountingTableQueryService tableQuery,
        IAccountingSqlQueryService sqlQuery,
        IAccountingSqlCommandService sqlCommand,
        IAccountingDataService dataService)
    {
        _schema = schema;
        _tableQuery = tableQuery;
        _sqlQuery = sqlQuery;
        _sqlCommand = sqlCommand;
        _dataService = dataService;
    }

    public async Task SyncPayrollPeriodStatusAsync(long payrollPeriodId)
    {
        if (payrollPeriodId <= 0) return;

        var periodRow = await _tableQuery.GetByIdAsync("payroll-periods", payrollPeriodId, null);
        if (periodRow is null) return;

        var currentStatus = AsString(periodRow, "status");
        if (currentStatus.Equals(PayrollPeriodStatusLocked, StringComparison.OrdinalIgnoreCase))
            return;

        var summary = (await _sqlQuery.QueryAsync(
            """
            SELECT
                COUNT(*) AS totalCount,
                SUM(CASE WHEN UPPER(ISNULL(status,'')) IN (@runStatusDraft,@runStatusCalculated) THEN 1 ELSE 0 END) AS draftCount,
                SUM(CASE WHEN UPPER(ISNULL(status,''))=@runStatusApproved THEN 1 ELSE 0 END) AS approvedCount,
                SUM(CASE WHEN UPPER(ISNULL(status,'')) IN (@runStatusPaid,@runStatusLocked) THEN 1 ELSE 0 END) AS paidCount
            FROM dbo.payroll_runs
            WHERE payroll_period_id=@periodId
              AND UPPER(ISNULL(status,'')) <> 'CANCELED';
            """,
            [
                new QueryParam("@periodId", payrollPeriodId),
                new QueryParam("@runStatusDraft", PayrollRunStatusDraft),
                new QueryParam("@runStatusCalculated", PayrollRunStatusCalculated),
                new QueryParam("@runStatusApproved", PayrollRunStatusApproved),
                new QueryParam("@runStatusPaid", PayrollRunStatusPaid),
                new QueryParam("@runStatusLocked", PayrollRunStatusLocked)
            ])).FirstOrDefault();

        var totalCount = Math.Max(0, AsInt(summary, "totalCount"));
        var draftCount = Math.Max(0, AsInt(summary, "draftCount"));
        var approvedCount = Math.Max(0, AsInt(summary, "approvedCount"));
        var paidCount = Math.Max(0, AsInt(summary, "paidCount"));

        var nextStatus = PayrollPeriodStatusOpen;
        if (totalCount > 0)
        {
            if (draftCount > 0)
            {
                nextStatus = PayrollPeriodStatusOpen;
            }
            else if (paidCount >= totalCount)
            {
                nextStatus = PayrollPeriodStatusPaid;
            }
            else if (approvedCount > 0 || paidCount > 0)
            {
                nextStatus = PayrollPeriodStatusApproved;
            }
        }

        if (currentStatus.Equals(nextStatus, StringComparison.OrdinalIgnoreCase))
            return;

        await _sqlCommand.ExecuteAsync(
            "UPDATE dbo.payroll_periods SET status=@status WHERE id=@id;",
            [
                new QueryParam("@status", nextStatus),
                new QueryParam("@id", payrollPeriodId)
            ]);
    }

    public async Task<PayrollRunResolution> ResolveOrCreateDraftPayrollRunAsync(long payrollPeriodId, long? branchId, long calculatedByUserId)
    {
        var existing = (await _sqlQuery.QueryAsync(
            """
            SELECT TOP 1 id, run_no
            FROM dbo.payroll_runs
            WHERE payroll_period_id=@payrollPeriodId
              AND (@branchId IS NULL OR branch_id=@branchId)
              AND UPPER(ISNULL(status,''))=@payrollRunStatusDraft
            ORDER BY run_no DESC, id DESC;
            """,
            [
                new QueryParam("@payrollPeriodId", payrollPeriodId),
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
                new QueryParam("@payrollRunStatusDraft", PayrollRunStatusDraft)
            ])).FirstOrDefault();

        var existingRunId = AsLong(existing, "id");
        if (existingRunId.HasValue && existingRunId.Value > 0)
        {
            var existingRunNo = AsLong(existing, "runNo");
            return new PayrollRunResolution(existingRunId.Value, existingRunNo.GetValueOrDefault(1), false);
        }

        var nextRunNo = await _sqlQuery.ScalarAsync<long?>(
            """
            SELECT ISNULL(MAX(run_no), 0) + 1
            FROM dbo.payroll_runs
            WHERE payroll_period_id=@payrollPeriodId
              AND (@branchId IS NULL OR branch_id=@branchId);
            """,
            [
                new QueryParam("@payrollPeriodId", payrollPeriodId),
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value)
            ]);
        var runNo = Math.Max(1, nextRunNo.GetValueOrDefault(1));

        var runId = await _sqlQuery.ScalarAsync<long?>(
            """
            DECLARE @inserted TABLE(id bigint);
            INSERT INTO dbo.payroll_runs
            (
                payroll_period_id,
                branch_id,
                run_no,
                status,
                calculated_by,
                calculated_at
            )
            OUTPUT INSERTED.id INTO @inserted(id)
            VALUES
            (
                @payrollPeriodId,
                @branchId,
                @runNo,
                @payrollRunStatusDraft,
                @calculatedBy,
                SYSDATETIME()
            );
            SELECT TOP 1 id FROM @inserted;
            """,
            [
                new QueryParam("@payrollPeriodId", payrollPeriodId),
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
                new QueryParam("@runNo", runNo),
                new QueryParam("@payrollRunStatusDraft", PayrollRunStatusDraft),
                new QueryParam("@calculatedBy", calculatedByUserId)
            ]);

        if (!runId.HasValue || runId.Value <= 0)
        {
            throw new InvalidOperationException("Failed to resolve payroll run for batch calculation.");
        }

        return new PayrollRunResolution(runId.Value, runNo, true);
    }

    public async Task<Dictionary<long, decimal>> LoadProfileBaseSalaryMapAsync(IReadOnlyCollection<long> staffIds, long? branchId, DateOnly asOfDate)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_schema.TryGetConfig("staff-salary-profiles", out var salaryCfg) || !await _schema.TableExistsAsync(salaryCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);
        var rows = await _sqlQuery.QueryAsync(
            $"""
            WITH ranked AS
            (
                SELECT
                    staff_id,
                    base_salary,
                    ROW_NUMBER() OVER (PARTITION BY staff_id ORDER BY effective_from DESC, id DESC) AS rn
                FROM dbo.staff_salary_profiles
                WHERE staff_id IN ({idList})
                  AND (@branchId IS NULL OR branch_id=@branchId OR branch_id IS NULL)
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status, @staffSalaryProfileStatusActive))=@staffSalaryProfileStatusActive
            )
            SELECT staff_id, base_salary
            FROM ranked
            WHERE rn=1;
            """,
            [
                new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
                new QueryParam("@asOf", asOf),
                new QueryParam("@staffSalaryProfileStatusActive", StaffSalaryProfileStatusActive)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            var baseSalary = AsDecimal(row, "baseSalary");
            if (baseSalary > 0m)
            {
                result[staffId.Value] = baseSalary;
            }
        }

        return result;
    }

    public async Task<Dictionary<long, ContractCompensationSnapshot>> LoadContractCompensationMapAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate)
    {
        var result = new Dictionary<long, ContractCompensationSnapshot>();
        if (staffIds.Count == 0) return result;
        if (!_schema.TryGetConfig("staff-contracts", out var contractCfg) || !await _schema.TableExistsAsync(contractCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);
        var rows = await _sqlQuery.QueryAsync(
            $"""
            WITH ranked AS
            (
                SELECT
                    staff_id,
                    base_salary,
                    allowance_amount,
                    ROW_NUMBER() OVER (PARTITION BY staff_id ORDER BY effective_from DESC, id DESC) AS rn
                FROM dbo.staff_contracts
                WHERE staff_id IN ({idList})
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status, @staffContractStatusActive))=@staffContractStatusActive
            )
            SELECT staff_id, base_salary, allowance_amount
            FROM ranked
            WHERE rn=1;
            """,
            [
                new QueryParam("@asOf", asOf),
                new QueryParam("@staffContractStatusActive", StaffContractStatusActive)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            var baseSalary = Math.Max(0m, AsDecimal(row, "baseSalary"));
            var allowanceAmount = Math.Max(0m, AsDecimal(row, "allowanceAmount"));
            result[staffId.Value] = new ContractCompensationSnapshot(baseSalary, allowanceAmount);
        }

        return result;
    }

    public async Task<Dictionary<long, decimal>> LoadStaffTierBaseSalaryMapAsync(IReadOnlyCollection<long> staffIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var staffIdSet = staffIds
            .Where(id => id > 0)
            .ToHashSet();
        if (staffIdSet.Count == 0) return result;

        return await _dataService.LoadStaffTierBaseSalariesAsync(staffIdSet, cancellationToken);
    }

    public async Task<Dictionary<long, decimal>> LoadOvertimeAmountMapAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_schema.TryGetConfig("overtime-entries", out var overtimeCfg) || !await _schema.TableExistsAsync(overtimeCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var fromDate = periodStartDate.ToDateTime(TimeOnly.MinValue);
        var toDate = periodEndDate.ToDateTime(TimeOnly.MaxValue);
        var rows = await _sqlQuery.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM(ISNULL(amount, 0)) AS overtime_amount
            FROM dbo.overtime_entries
            WHERE staff_id IN ({idList})
              AND work_date >= @fromDate
              AND work_date <= @toDate
              AND (
                    status IS NULL
                    OR LTRIM(RTRIM(ISNULL(status, ''))) = ''
                    OR UPPER(ISNULL(status, '')) IN (@adjustmentApprovedStatus, @adjustmentPaidStatus, @journalPostedStatus, @paymentCompletedStatus)
                  )
            GROUP BY staff_id;
            """,
            [
                new QueryParam("@fromDate", fromDate),
                new QueryParam("@toDate", toDate),
                new QueryParam("@adjustmentApprovedStatus", AdjustmentApprovedStatus),
                new QueryParam("@adjustmentPaidStatus", AdjustmentPaidStatus),
                new QueryParam("@journalPostedStatus", JournalStatusPosted),
                new QueryParam("@paymentCompletedStatus", PaymentCompletedStatus)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "overtimeAmount"));
        }

        return result;
    }

    public async Task<Dictionary<long, decimal>> LoadAttendanceDeductionMapAsync(IReadOnlyCollection<long> staffIds, DateOnly periodStartDate, DateOnly periodEndDate)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_schema.TryGetConfig("attendance-adjustments", out var attendanceCfg) || !await _schema.TableExistsAsync(attendanceCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var fromDate = periodStartDate.ToDateTime(TimeOnly.MinValue);
        var toDate = periodEndDate.ToDateTime(TimeOnly.MaxValue);
        var rows = await _sqlQuery.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM
                (
                    CASE
                        WHEN amount_impact IS NULL THEN 0
                        WHEN amount_impact < 0 THEN ABS(amount_impact)
                        WHEN UPPER(ISNULL(adjustment_type, '')) IN ('DEDUCTION', 'ABSENT', 'LATE', 'UNPAID_LEAVE', 'PENALTY') THEN amount_impact
                        ELSE 0
                    END
                ) AS attendance_deduction
            FROM dbo.attendance_adjustments
            WHERE staff_id IN ({idList})
              AND work_date >= @fromDate
              AND work_date <= @toDate
              AND (
                    status IS NULL
                    OR LTRIM(RTRIM(ISNULL(status, ''))) = ''
                    OR UPPER(ISNULL(status, '')) IN (@adjustmentApprovedStatus, @adjustmentPaidStatus, @journalPostedStatus, @paymentCompletedStatus)
                  )
            GROUP BY staff_id;
            """,
            [
                new QueryParam("@fromDate", fromDate),
                new QueryParam("@toDate", toDate),
                new QueryParam("@adjustmentApprovedStatus", AdjustmentApprovedStatus),
                new QueryParam("@adjustmentPaidStatus", AdjustmentPaidStatus),
                new QueryParam("@journalPostedStatus", JournalStatusPosted),
                new QueryParam("@paymentCompletedStatus", PaymentCompletedStatus)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "attendanceDeduction"));
        }

        return result;
    }

    public async Task<Dictionary<long, decimal>> LoadSalaryAdvanceDeductionMapAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_schema.TryGetConfig("salary-advances", out var advanceCfg) || !await _schema.TableExistsAsync(advanceCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var rows = await _sqlQuery.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM(ISNULL(amount, 0)) AS advance_deduction
            FROM dbo.salary_advances
            WHERE staff_id IN ({idList})
              AND deduct_from_payroll_period_id=@payrollPeriodId
              AND (
                    status IS NULL
                    OR LTRIM(RTRIM(ISNULL(status, ''))) = ''
                    OR UPPER(ISNULL(status, '')) IN (@salaryAdvanceStatusApproved, @salaryAdvanceStatusDeducting)
                  )
            GROUP BY staff_id;
            """,
            [
                new QueryParam("@payrollPeriodId", payrollPeriodId),
                new QueryParam("@salaryAdvanceStatusApproved", SalaryAdvanceStatusApproved),
                new QueryParam("@salaryAdvanceStatusDeducting", SalaryAdvanceStatusDeducting)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "advanceDeduction"));
        }

        return result;
    }

    public async Task<Dictionary<long, decimal>> LoadLoanDeductionMapAsync(IReadOnlyCollection<long> staffIds, long payrollPeriodId)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;
        if (!_schema.TryGetConfig("employee-loans", out var loanCfg) || !await _schema.TableExistsAsync(loanCfg))
            return result;

        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var rows = await _sqlQuery.QueryAsync(
            $"""
            SELECT
                staff_id,
                SUM
                (
                    CASE
                        WHEN ISNULL(monthly_deduction, 0) > 0
                            THEN CASE
                                     WHEN ISNULL(outstanding_amount, 0) > 0 AND monthly_deduction > outstanding_amount THEN outstanding_amount
                                     ELSE monthly_deduction
                                 END
                        ELSE ISNULL(outstanding_amount, 0)
                    END
                ) AS loan_deduction
            FROM dbo.employee_loans
            WHERE staff_id IN ({idList})
              AND (start_payroll_period_id IS NULL OR start_payroll_period_id <= @payrollPeriodId)
              AND ISNULL(outstanding_amount, 0) > 0
              AND UPPER(ISNULL(status, @employeeLoanStatusActive)) = @employeeLoanStatusActive
            GROUP BY staff_id;
            """,
            [
                new QueryParam("@payrollPeriodId", payrollPeriodId),
                new QueryParam("@employeeLoanStatusActive", EmployeeLoanStatusActive)
            ]);

        foreach (var row in rows)
        {
            var staffId = AsLong(row, "staffId");
            if (!staffId.HasValue || staffId.Value <= 0) continue;
            result[staffId.Value] = Math.Max(0m, AsDecimal(row, "loanDeduction"));
        }

        return result;
    }

    public async Task<Dictionary<long, int>> LoadDependentCountMapAsync(IReadOnlyCollection<long> staffIds, DateOnly asOfDate)
    {
        var result = new Dictionary<long, int>();
        if (staffIds.Count == 0) return result;
        var idList = BuildLongListSqlLiteral(staffIds);
        if (idList == "0") return result;

        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);

        if (_schema.TryGetConfig("pit-profiles", out var pitCfg) && await _schema.TableExistsAsync(pitCfg))
        {
            var pitRows = await _sqlQuery.QueryAsync(
                $"""
                WITH ranked AS
                (
                    SELECT
                        staff_id,
                        dependent_count,
                        ROW_NUMBER() OVER (PARTITION BY staff_id ORDER BY effective_from DESC, id DESC) AS rn
                    FROM dbo.pit_profiles
                    WHERE staff_id IN ({idList})
                      AND (effective_from IS NULL OR effective_from <= @asOf)
                      AND (effective_to IS NULL OR effective_to >= @asOf)
                )
                SELECT staff_id, dependent_count
                FROM ranked
                WHERE rn=1;
                """,
                [new QueryParam("@asOf", asOf)]);

            foreach (var row in pitRows)
            {
                var staffId = AsLong(row, "staffId");
                if (!staffId.HasValue || staffId.Value <= 0) continue;
                result[staffId.Value] = Math.Max(0, AsInt(row, "dependentCount"));
            }
        }

        if (_schema.TryGetConfig("employee-dependents", out var dependentCfg) && await _schema.TableExistsAsync(dependentCfg))
        {
            var dependentRows = await _sqlQuery.QueryAsync(
                $"""
                SELECT
                    staff_id,
                    COUNT(*) AS dependent_count
                FROM dbo.employee_dependents
                WHERE staff_id IN ({idList})
                  AND (effective_from IS NULL OR effective_from <= @asOf)
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                GROUP BY staff_id;
                """,
                [new QueryParam("@asOf", asOf)]);

            foreach (var row in dependentRows)
            {
                var staffId = AsLong(row, "staffId");
                if (!staffId.HasValue || staffId.Value <= 0) continue;
                if (result.ContainsKey(staffId.Value)) continue;
                result[staffId.Value] = Math.Max(0, AsInt(row, "dependentCount"));
            }
        }

        return result;
    }

    private static string BuildLongListSqlLiteral(IEnumerable<long> values)
    {
        var normalized = values
            .Where(value => value > 0)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        return normalized.Length == 0 ? "0" : string.Join(", ", normalized);
    }

    private static string AsString(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null ? Convert.ToString(v) ?? "" : "";

    private static long? AsLong(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt64(v) : null;

    private static decimal AsDecimal(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0m;

    private static int AsInt(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToInt32(v) : 0;
}

public sealed record PayrollRunResolution(long RunId, long RunNo, bool CreatedNew);
public sealed record ContractCompensationSnapshot(decimal BaseSalary, decimal AllowanceAmount);
