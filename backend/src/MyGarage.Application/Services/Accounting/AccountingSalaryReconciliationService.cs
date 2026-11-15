using System.Globalization;
using System.Text.Json;
using MyGarage.Application.Common;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Application.Accounting;

public sealed class AccountingSalaryReconciliationService : IAccountingSalaryReconciliationService
{
    private const string FinanceApprovalStatusPending = nameof(FinanceApprovalRequestStatusCode.PENDING);
    private const string CommissionStatusPending = nameof(CommissionTransactionStatusCode.PENDING);
    private const string CommissionStatusApproved = nameof(CommissionTransactionStatusCode.APPROVED);
    private const string CommissionStatusProcessed = nameof(CommissionTransactionStatusCode.PROCESSED);
    private const string CommissionStatusPaid = nameof(CommissionTransactionStatusCode.PAID);
    private const string CommissionStatusCanceled = nameof(CommissionTransactionStatusCode.CANCELED);
    private const string StaffSalaryProfileStatusActive = nameof(StaffSalaryProfileStatusCode.ACTIVE);
    private const string StaffContractStatusActive = nameof(StaffContractStatusCode.ACTIVE);

    private readonly IAccountingSchemaService _schema;
    private readonly IAccountingSqlQueryService _sqlQuery;
    private readonly IAccountingDataService _dataService;
    private readonly IAccountingPayrollPolicyCalculator _payrollPolicyCalculator;

    public AccountingSalaryReconciliationService(
        IAccountingSchemaService schema,
        IAccountingSqlQueryService sqlQuery,
        IAccountingDataService dataService,
        IAccountingPayrollPolicyCalculator payrollPolicyCalculator)
    {
        _schema = schema;
        _sqlQuery = sqlQuery;
        _dataService = dataService;
        _payrollPolicyCalculator = payrollPolicyCalculator;
    }

    public async Task<AccountingCommandResult> GetPayrollRunCommissionReconciliationAsync(
        long id,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reconciliation = await BuildPayrollRunCommissionReconciliationAsync(id, branchId, cancellationToken);
            return new AccountingCommandResult(200, reconciliation);
        }
        catch (KeyNotFoundException ex)
        {
            return new AccountingCommandResult(404, new { message = ex.Message });
        }
    }

    public async Task<AccountingCommandResult> GetStaffBaseSalaryAsync(
        long staffId,
        DateTime? asOfDate,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        var staff = await _dataService.GetActiveStaffAsync(staffId, cancellationToken);
        if (staff is null) return new AccountingCommandResult(404, new { message = $"Staff #{staffId} not found." });
        if (branchId.HasValue && staff.BranchId != branchId.Value) return new AccountingCommandResult(404);

        var asOf = DateOnly.FromDateTime((asOfDate ?? VietnamTime.Now).Date);
        var baseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, branchId, asOf);

        return new AccountingCommandResult(200, new
        {
            staffId = staff.Id,
            staffName = staff.FullName,
            branchId = staff.BranchId,
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            baseSalary
        });
    }

    public async Task<AccountingCommandResult> GetStaffSalaryCoefficientAsync(
        long staffId,
        DateTime? asOfDate,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        var staff = await _dataService.GetActiveStaffAsync(staffId, cancellationToken);
        if (staff is null) return new AccountingCommandResult(404, new { message = $"Staff #{staffId} not found." });
        if (branchId.HasValue && staff.BranchId != branchId.Value) return new AccountingCommandResult(404);

        var hireDate = await ResolveStaffHireDateAsync(staff.Id, staff.HireDate);
        var asOf = DateOnly.FromDateTime((asOfDate ?? VietnamTime.Now).Date);
        decimal coefficient = 1.0000m;
        int yearsOfService = 0;

        if (hireDate.HasValue && asOf >= hireDate.Value)
        {
            yearsOfService = _payrollPolicyCalculator.CalculateCompletedYears(hireDate.Value, asOf);
            coefficient = await ResolveStaffSalaryCoefficientAsync(staff.Id, hireDate.Value, asOf, cancellationToken);
        }

        var baseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, branchId, asOf);
        var hasBaseSalary = baseSalary.HasValue && baseSalary.Value > 0m;
        decimal calculatedSalary = hasBaseSalary
            ? Math.Round(baseSalary!.Value * coefficient, 0, MidpointRounding.AwayFromZero)
            : 0m;

        return new AccountingCommandResult(200, new
        {
            staffId = staff.Id,
            staffName = staff.FullName,
            branchId = staff.BranchId,
            hireDate = hireDate?.ToString("yyyy-MM-dd") ?? "",
            asOfDate = asOf.ToString("yyyy-MM-dd"),
            yearsOfService,
            baseSalary = hasBaseSalary ? baseSalary!.Value : 0m,
            hasBaseSalary,
            coefficient,
            calculatedSalary,
            policyReference = "Nearest active threshold in SalaryCoefficients where YearsOfService <= completed years; default 1.0 when no threshold is configured."
        });
    }

    public async Task<AccountingCommandResult> RequestInitialSalaryAsync(
        long staffId,
        InitialSalaryCommand command,
        long userId,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        var requestedBaseSalary = command.BaseSalary.GetValueOrDefault();
        if (requestedBaseSalary <= 0m)
        {
            return new AccountingCommandResult(400, new { message = "baseSalary must be greater than 0." });
        }

        if (!_schema.TryGetConfig("finance-approval-requests", out var approvalCfg) || !await _schema.TableExistsAsync(approvalCfg))
        {
            return new AccountingCommandResult(400, new { message = "finance_approval_requests table is required for salary approval flow." });
        }

        var staff = await _dataService.GetActiveStaffAsync(staffId, cancellationToken);
        if (staff is null) return new AccountingCommandResult(404, new { message = $"Staff #{staffId} not found." });
        if (branchId.HasValue && staff.BranchId != branchId.Value) return new AccountingCommandResult(404);

        var effectiveFromDate = DateOnly.FromDateTime((command.EffectiveFrom ?? VietnamTime.Now).Date);
        var existingBaseSalary = await ResolveStaffBaseSalaryAsync(staff.Id, branchId, effectiveFromDate);
        if (existingBaseSalary.HasValue && existingBaseSalary.Value > 0m)
        {
            return new AccountingCommandResult(400, new { message = "Staff already has an active base salary profile for the selected effective date." });
        }

        var pendingRequest = (await _sqlQuery.QueryAsync(
            """
            SELECT TOP 1 id
            FROM dbo.finance_approval_requests
            WHERE request_type='PAYROLL_ADJUSTMENT'
              AND JSON_VALUE(request_payload, '$.requestSubtype') = 'STAFF_INITIAL_SALARY'
              AND status=@statusPending
              AND TRY_CONVERT(bigint, JSON_VALUE(request_payload, '$.staffId')) = @staffId
            ORDER BY id DESC;
            """,
            [
                new QueryParam("@staffId", staff.Id),
                new QueryParam("@statusPending", FinanceApprovalStatusPending)
            ])).FirstOrDefault();
        if (pendingRequest is not null)
        {
            return new AccountingCommandResult(409, new { message = "A pending initial salary request for this staff already exists." });
        }

        var payloadObject = new
        {
            requestType = "PAYROLL_ADJUSTMENT",
            requestSubtype = "STAFF_INITIAL_SALARY",
            staffId = staff.Id,
            staffName = staff.FullName,
            branchId = staff.BranchId,
            baseSalary = requestedBaseSalary,
            effectiveFrom = effectiveFromDate.ToString("yyyy-MM-dd"),
            note = command.Note?.Trim() ?? "",
            requestedBy = userId,
            requestedAt = VietnamTime.Now.ToString("O")
        };
        var payloadJson = JsonSerializer.Serialize(payloadObject);
        var reason = string.IsNullOrWhiteSpace(command.Reason)
            ? $"Yêu cầu tạo lương khởi điểm cho nhân sự {staff.FullName} (#{staff.Id})."
            : command.Reason.Trim();

        var createdId = await _sqlQuery.ScalarAsync<long?>(
            """
            DECLARE @inserted TABLE (id bigint);
            INSERT INTO dbo.finance_approval_requests
            (
                request_type,
                requested_by,
                approver_id,
                status,
                risk_level,
                amount,
                reason,
                reject_reason,
                request_payload,
                created_at,
                updated_at
            )
            OUTPUT INSERTED.id INTO @inserted(id)
            VALUES
            (
                'PAYROLL_ADJUSTMENT',
                @requestedBy,
                NULL,
                @statusPending,
                'MEDIUM',
                @amount,
                @reason,
                NULL,
                @payload,
                SYSDATETIME(),
                SYSDATETIME()
            );
            SELECT TOP 1 id FROM @inserted;
            """,
            [
                new QueryParam("@requestedBy", userId),
                new QueryParam("@statusPending", FinanceApprovalStatusPending),
                new QueryParam("@amount", requestedBaseSalary),
                new QueryParam("@reason", reason),
                new QueryParam("@payload", payloadJson)
            ]);

        if (!createdId.HasValue)
        {
            return new AccountingCommandResult(400, new { message = "Failed to create initial salary approval request." });
        }

        return new AccountingCommandResult(200, new
        {
            requestId = createdId.Value,
            status = FinanceApprovalStatusPending,
            staffId = staff.Id,
            staffName = staff.FullName,
            baseSalary = requestedBaseSalary,
            effectiveFrom = effectiveFromDate.ToString("yyyy-MM-dd"),
            message = "Initial salary request has been sent to admin for approval."
        });
    }

    public async Task<PayrollRunCommissionReconciliationResponse> BuildPayrollRunCommissionReconciliationAsync(
        long payrollRunId,
        long? accessBranchId,
        CancellationToken cancellationToken = default)
    {
        var run = await _dataService.GetPayrollRunWithPeriodAsync(payrollRunId, cancellationToken);
        if (run is null) throw new KeyNotFoundException($"Payroll run #{payrollRunId} not found.");
        if (accessBranchId.HasValue && run.BranchId.HasValue && run.BranchId.Value != accessBranchId.Value)
            throw new KeyNotFoundException($"Payroll run #{payrollRunId} not found.");

        var periodStart = run.PayrollPeriod.StartDate.ToDateTime(TimeOnly.MinValue);
        var periodEnd = run.PayrollPeriod.EndDate;
        var periodEndExclusive = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var payrollItems = await _dataService.GetPayrollItemCommissionsAsync(payrollRunId, cancellationToken);
        var staffIds = payrollItems.Select(x => x.StaffId).Distinct().ToHashSet();

        var transactions = await _dataService.GetPositiveCommissionTransactionsAsync(staffIds, cancellationToken);

        var details = new List<PayrollCommissionReconciliationDetail>();
        foreach (var byStaff in payrollItems.GroupBy(x => x.StaffId))
        {
            var staffId = byStaff.Key;
            var payrollCommissionAmount = Math.Round(byStaff.Sum(x => Math.Max(0m, x.CommissionAmount)), 0, MidpointRounding.AwayFromZero);

            var staffTransactions = transactions
                .Where(x => x.StaffId == staffId)
                .Where(x =>
                {
                    var effectiveAt = x.ApprovedAt ?? x.CreatedAt ?? x.UpdatedAt;
                    return effectiveAt.HasValue && effectiveAt.Value >= periodStart && effectiveAt.Value < periodEndExclusive;
                })
                .ToList();

            decimal processedAmount = 0m;
            decimal pendingAmount = 0m;
            decimal paidAmount = 0m;
            decimal canceledAmount = 0m;
            var processedCount = 0;
            var pendingCount = 0;
            var paidCount = 0;
            var canceledCount = 0;

            foreach (var tx in staffTransactions)
            {
                var amount = Math.Max(0m, tx.CommissionAmount);
                var status = (tx.Status ?? string.Empty).Trim().ToUpperInvariant();
                switch (status)
                {
                    case CommissionStatusProcessed:
                        processedAmount += amount;
                        processedCount++;
                        break;
                    case CommissionStatusPending:
                    case CommissionStatusApproved:
                        pendingAmount += amount;
                        pendingCount++;
                        break;
                    case CommissionStatusPaid:
                        paidAmount += amount;
                        paidCount++;
                        break;
                    case CommissionStatusCanceled:
                        canceledAmount += amount;
                        canceledCount++;
                        break;
                }
            }

            var gapAmount = Math.Round(payrollCommissionAmount - Math.Round(processedAmount, 0, MidpointRounding.AwayFromZero), 0, MidpointRounding.AwayFromZero);
            details.Add(new PayrollCommissionReconciliationDetail
            {
                StaffId = staffId,
                PayrollCommissionAmount = payrollCommissionAmount,
                ProcessedAmount = Math.Round(processedAmount, 0, MidpointRounding.AwayFromZero),
                PendingAmount = Math.Round(pendingAmount, 0, MidpointRounding.AwayFromZero),
                PaidAmount = Math.Round(paidAmount, 0, MidpointRounding.AwayFromZero),
                CanceledAmount = Math.Round(canceledAmount, 0, MidpointRounding.AwayFromZero),
                ProcessedCount = processedCount,
                PendingCount = pendingCount,
                PaidCount = paidCount,
                CanceledCount = canceledCount,
                GapAmount = gapAmount,
                IsMatched = gapAmount == 0m
            });
        }

        var totalPayrollCommission = details.Sum(x => x.PayrollCommissionAmount);
        var totalProcessed = details.Sum(x => x.ProcessedAmount);
        var totalPending = details.Sum(x => x.PendingAmount);
        var totalPaid = details.Sum(x => x.PaidAmount);
        var totalCanceled = details.Sum(x => x.CanceledAmount);
        var totalGap = details.Sum(x => x.GapAmount);

        return new PayrollRunCommissionReconciliationResponse
        {
            PayrollRun = new PayrollRunCommissionReconciliationRunInfo
            {
                Id = run.Id,
                RunNo = run.RunNo,
                Status = run.Status,
                BranchId = run.BranchId,
                PayrollPeriodId = run.PayrollPeriodId,
                PeriodCode = run.PayrollPeriod.PeriodCode,
                PeriodStartDate = run.PayrollPeriod.StartDate.ToString("yyyy-MM-dd"),
                PeriodEndDate = run.PayrollPeriod.EndDate.ToString("yyyy-MM-dd")
            },
            Summary = new PayrollRunCommissionReconciliationSummary
            {
                StaffCount = details.Count,
                TotalPayrollCommission = totalPayrollCommission,
                TotalProcessed = totalProcessed,
                TotalPending = totalPending,
                TotalPaid = totalPaid,
                TotalCanceled = totalCanceled,
                TotalGap = totalGap,
                IsFullyMatched = totalGap == 0m
            },
            Details = details
        };
    }

    private async Task<decimal?> ResolveStaffBaseSalaryAsync(long staffId, long? branchId, DateOnly asOfDate)
    {
        var asOf = asOfDate.ToDateTime(TimeOnly.MinValue);

        if (_schema.TryGetConfig("staff-salary-profiles", out var salaryCfg) && await _schema.TableExistsAsync(salaryCfg))
        {
            var salaryRow = (await _sqlQuery.QueryAsync(
                """
                SELECT TOP 1 base_salary
                FROM dbo.staff_salary_profiles
                WHERE staff_id=@staffId
                  AND (@branchId IS NULL OR branch_id=@branchId OR branch_id IS NULL)
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status,@staffSalaryProfileStatusActive))=@staffSalaryProfileStatusActive
                ORDER BY effective_from DESC, id DESC;
                """,
                [
                    new QueryParam("@staffId", staffId),
                    new QueryParam("@branchId", (object?)branchId ?? DBNull.Value),
                    new QueryParam("@asOf", asOf),
                    new QueryParam("@staffSalaryProfileStatusActive", StaffSalaryProfileStatusActive)
                ])).FirstOrDefault();
            var baseSalary = AsDecimal(salaryRow, "baseSalary");
            if (baseSalary > 0m) return baseSalary;
        }

        if (_schema.TryGetConfig("staff-contracts", out var contractCfg) && await _schema.TableExistsAsync(contractCfg))
        {
            var contractRow = (await _sqlQuery.QueryAsync(
                """
                SELECT TOP 1 base_salary
                FROM dbo.staff_contracts
                WHERE staff_id=@staffId
                  AND effective_from <= @asOf
                  AND (effective_to IS NULL OR effective_to >= @asOf)
                  AND UPPER(ISNULL(status,@staffContractStatusActive))=@staffContractStatusActive
                ORDER BY effective_from DESC, id DESC;
                """,
                [
                    new QueryParam("@staffId", staffId),
                    new QueryParam("@asOf", asOf),
                    new QueryParam("@staffContractStatusActive", StaffContractStatusActive)
                ])).FirstOrDefault();
            var baseSalary = AsDecimal(contractRow, "baseSalary");
            if (baseSalary > 0m) return baseSalary;
        }

        return null;
    }

    private async Task<DateOnly?> ResolveStaffHireDateAsync(long staffId, DateOnly? currentHireDate)
    {
        if (currentHireDate.HasValue) return currentHireDate.Value;

        var staffRow = (await _sqlQuery.QueryAsync(
            """
            IF OBJECT_ID('dbo.Staffs', 'U') IS NULL
            BEGIN
                SELECT CAST(NULL AS date) AS hire_date;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'HireDate') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(HireDate AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'hire_date') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(hire_date AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'CreatedAt') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(CreatedAt AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE IF COL_LENGTH('dbo.Staffs', 'created_at') IS NOT NULL
            BEGIN
                SELECT TOP 1 CAST(created_at AS date) AS hire_date
                FROM dbo.Staffs
                WHERE Id=@staffId;
            END
            ELSE
            BEGIN
                SELECT CAST(NULL AS date) AS hire_date;
            END
            """,
            [new QueryParam("@staffId", staffId)])).FirstOrDefault();
        var hireDate = AsDateOnly(staffRow, "hireDate");
        if (hireDate.HasValue) return hireDate;

        if (_schema.TryGetConfig("staff-contracts", out var contractCfg) && await _schema.TableExistsAsync(contractCfg))
        {
            var contractRow = (await _sqlQuery.QueryAsync(
                """
                SELECT TOP 1 effective_from AS hire_date
                FROM dbo.staff_contracts
                WHERE staff_id=@staffId
                  AND effective_from IS NOT NULL
                ORDER BY effective_from ASC, id ASC;
                """,
                [new QueryParam("@staffId", staffId)])).FirstOrDefault();
            hireDate = AsDateOnly(contractRow, "hireDate");
            if (hireDate.HasValue) return hireDate;
        }

        if (_schema.TryGetConfig("staff-salary-profiles", out var salaryCfg) && await _schema.TableExistsAsync(salaryCfg))
        {
            var salaryRow = (await _sqlQuery.QueryAsync(
                """
                SELECT TOP 1 effective_from AS hire_date
                FROM dbo.staff_salary_profiles
                WHERE staff_id=@staffId
                  AND effective_from IS NOT NULL
                ORDER BY effective_from ASC, id ASC;
                """,
                [new QueryParam("@staffId", staffId)])).FirstOrDefault();
            hireDate = AsDateOnly(salaryRow, "hireDate");
            if (hireDate.HasValue) return hireDate;
        }

        return null;
    }

    private async Task<decimal> ResolveStaffSalaryCoefficientAsync(
        long staffId,
        DateOnly hireDate,
        DateOnly asOfDate,
        CancellationToken cancellationToken)
    {
        _ = staffId;
        var yearsOfService = _payrollPolicyCalculator.CalculateCompletedYears(hireDate, asOfDate);
        var configuredCoefficient = await _dataService.GetSalaryCoefficientAsync(yearsOfService, cancellationToken);

        if (configuredCoefficient.HasValue && configuredCoefficient.Value > 0m)
        {
            return configuredCoefficient.Value;
        }

        return 1.0000m;
    }

    private static decimal AsDecimal(IDictionary<string, object?>? row, string key) =>
        row != null && row.TryGetValue(key, out var v) && v != null && v != DBNull.Value ? Convert.ToDecimal(v) : 0m;

    private static DateOnly? AsDateOnly(IDictionary<string, object?>? row, string key)
    {
        if (row == null || !row.TryGetValue(key, out var v) || v == null || v == DBNull.Value) return null;
        if (v is DateOnly dateOnly) return dateOnly;
        if (v is DateTime dateTime) return DateOnly.FromDateTime(dateTime);
        if (v is DateTimeOffset dateTimeOffset) return DateOnly.FromDateTime(dateTimeOffset.DateTime);

        var text = Convert.ToString(v, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateOnly)) return parsedDateOnly;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime)) return DateOnly.FromDateTime(parsedDateTime);
        return null;
    }
}
