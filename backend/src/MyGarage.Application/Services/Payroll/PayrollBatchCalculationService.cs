using MyGarage.Application.Payroll;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.PayrollBatchCalculationHelpers;

namespace MyGarage.Services;

public sealed class PayrollBatchCalculationService : IPayrollBatchCalculationService
{
    private readonly IPayrollStore _store;
    private readonly IPayrollEmployeeCalculationService _employeeCalculationService;

    public PayrollBatchCalculationService(
        IPayrollStore store,
        IPayrollEmployeeCalculationService employeeCalculationService)
    {
        _store = store;
        _employeeCalculationService = employeeCalculationService;
    }

    public async Task<PayrollBatchCalculationResult> CalculatePayrollForAllAsync(
        PayrollBatchCalculationCommand command,
        CancellationToken cancellationToken = default)
    {
        var now = VietnamTime.Now;
        var period = await ResolvePayrollPeriodAsync(command, cancellationToken);

        if (!string.Equals(NormalizeKey(period.Status), PayrollPeriodStatusOpen, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Batch payroll calculation only supports OPEN payroll period.");
        }

        var periodStartDate = period.StartDate;
        var periodEndDate = period.EndDate != default ? period.EndDate : period.PayrollDate;
        if (periodEndDate < periodStartDate)
        {
            throw new InvalidOperationException("Payroll period date range is invalid.");
        }

        return await _store.ExecuteInTransactionAsync(async ct =>
        {
            var runResolution = await ResolveOrCreateDraftPayrollRunAsync(
                period.Id,
                command.BranchId,
                command.UserId,
                command.PayrollRunId,
                ct);

            var staffRows = (await _store.GetPayrollStaffSnapshotsAsync(command.BranchId, ct)).ToList();
            if (!command.IncludeInactiveStaff)
            {
                staffRows = staffRows
                    .Where(s => IsPayrollActiveStaff(s, periodEndDate))
                    .ToList();
            }

            if (staffRows.Count == 0)
            {
                runResolution.Run.CalculatedBy = command.UserId;
                runResolution.Run.CalculatedAt = now;
                runResolution.Run.Status = PayrollRunStatusCalculated;
                runResolution.Run.UpdatedAt = now;
                await _store.SaveChangesAsync(ct);

                return new PayrollBatchCalculationResult
                {
                    PayrollPeriodId = period.Id,
                    PayrollRunId = runResolution.Run.Id,
                    RunNo = runResolution.Run.RunNo,
                    CreatedRun = runResolution.CreatedNew,
                    PeriodStartDate = periodStartDate.ToString("yyyy-MM-dd"),
                    PeriodEndDate = periodEndDate.ToString("yyyy-MM-dd"),
                    ProcessedStaffCount = 0,
                    CalculatedStaffCount = 0,
                    Inserted = 0,
                    Updated = 0,
                    SkippedNoBaseSalary = 0,
                    SkippedExisting = 0,
                    IssuePayslips = command.IssuePayslips,
                    PayslipsIssued = 0,
                    GrossTotal = 0m,
                    NetTotal = 0m,
                    Message = "Batch payroll calculation completed."
                };
            }

            var output = await _employeeCalculationService.CalculateAndApplyAsync(
                new PayrollEmployeeBatchCalculationInput(
                    PayrollPeriodId: period.Id,
                    PayrollRunId: runResolution.Run.Id,
                    BranchId: command.BranchId,
                    PeriodStartDate: periodStartDate,
                    PeriodEndDate: periodEndDate,
                    OverwriteExisting: command.OverwriteExisting,
                    Now: now,
                    StaffRows: staffRows),
                ct);

            runResolution.Run.CalculatedBy = command.UserId;
            runResolution.Run.CalculatedAt = now;
            runResolution.Run.Status = PayrollRunStatusCalculated;
            runResolution.Run.UpdatedAt = now;

            await _store.SaveChangesAsync(ct);
            var processedCommissionTransactions = await MarkCommissionTransactionsProcessedAsync(
                output.ProcessedCommissionIds,
                command.UserId,
                now,
                ct);

            var payslipsIssued = 0;
            if (command.IssuePayslips)
            {
                payslipsIssued = await IssuePayslipsAsync(
                    runResolution.Run.RunNo,
                    now,
                    command.UserId,
                    output.AffectedItems,
                    ct);
            }

            return new PayrollBatchCalculationResult
            {
                PayrollPeriodId = period.Id,
                PayrollRunId = runResolution.Run.Id,
                RunNo = runResolution.Run.RunNo,
                CreatedRun = runResolution.CreatedNew,
                PeriodStartDate = periodStartDate.ToString("yyyy-MM-dd"),
                PeriodEndDate = periodEndDate.ToString("yyyy-MM-dd"),
                ProcessedStaffCount = staffRows.Count,
                CalculatedStaffCount = output.Inserted + output.Updated,
                Inserted = output.Inserted,
                Updated = output.Updated,
                SkippedNoBaseSalary = output.SkippedNoBaseSalary,
                SkippedExisting = output.SkippedExisting,
                IssuePayslips = command.IssuePayslips,
                PayslipsIssued = payslipsIssued,
                GrossTotal = output.GrossTotal,
                NetTotal = output.NetTotal,
                CommissionTotal = output.CommissionTotal,
                ProcessedCommissionTransactions = processedCommissionTransactions,
                Message = "Batch payroll calculation completed."
            };
        }, cancellationToken);
    }

    private async Task<PayrollPeriod> ResolvePayrollPeriodAsync(PayrollBatchCalculationCommand command, CancellationToken cancellationToken)
    {
        if (command.PayrollPeriodId.HasValue && command.PayrollPeriodId.Value > 0)
        {
            var selected = await _store.GetPayrollPeriodByIdAsync(command.PayrollPeriodId.Value, cancellationToken);
            if (selected is null)
            {
                throw new KeyNotFoundException($"Payroll period #{command.PayrollPeriodId.Value} not found.");
            }

            return selected;
        }

        var openPeriod = await _store.GetLatestOpenPayrollPeriodAsync(cancellationToken);

        if (openPeriod is null)
        {
            throw new InvalidOperationException("No OPEN payroll period found. Please create/open one first.");
        }

        return openPeriod;
    }

    private async Task<PayrollRunResolution> ResolveOrCreateDraftPayrollRunAsync(
        long payrollPeriodId,
        long? branchId,
        long calculatedByUserId,
        long? requestedRunId,
        CancellationToken cancellationToken)
    {
        if (requestedRunId.HasValue && requestedRunId.Value > 0)
        {
            var provided = await _store.GetPayrollRunByIdAsync(requestedRunId.Value, track: true, cancellationToken);
            if (provided is null || (branchId.HasValue && provided.BranchId != branchId.Value))
            {
                throw new KeyNotFoundException($"Payroll run #{requestedRunId.Value} not found.");
            }

            if (provided.PayrollPeriodId != payrollPeriodId)
            {
                throw new InvalidOperationException("Selected payroll run does not belong to the selected payroll period.");
            }

            var runStatus = NormalizeKey(provided.Status);
            if (runStatus is not PayrollRunStatusDraft and not PayrollRunStatusCalculated)
            {
                throw new InvalidOperationException("Batch calculation can only update a DRAFT/CALCULATED payroll run.");
            }

            return new PayrollRunResolution(provided, false);
        }

        var existingDraft = await _store.GetLatestDraftPayrollRunAsync(payrollPeriodId, branchId, cancellationToken);
        if (existingDraft is not null)
        {
            return new PayrollRunResolution(existingDraft, false);
        }

        var maxRunNo = await _store.GetMaxRunNoForPeriodAsync(payrollPeriodId, branchId, cancellationToken);
        var nextRunNo = Math.Max(1, maxRunNo + 1);

        var now = VietnamTime.Now;
        var run = new PayrollRun
        {
            PayrollPeriodId = payrollPeriodId,
            BranchId = branchId,
            RunNo = nextRunNo,
            Status = PayrollRunStatusDraft,
            CalculatedBy = calculatedByUserId,
            CalculatedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _store.AddPayrollRunAsync(run, cancellationToken);
        return new PayrollRunResolution(run, true);
    }

    private async Task<int> IssuePayslipsAsync(
        int runNo,
        DateTime now,
        long issuedBy,
        IReadOnlyCollection<PayrollItem> affectedItems,
        CancellationToken cancellationToken)
    {
        var affectedItemIds = affectedItems
            .Select(x => x.Id)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (affectedItemIds.Count == 0) return 0;

        var existingSet = await _store.GetExistingPayslipItemIdsAsync(affectedItemIds, cancellationToken);

        var newPayslips = new List<Payslip>();
        foreach (var payrollItemId in affectedItemIds)
        {
            if (existingSet.Contains(payrollItemId))
            {
                continue;
            }

            newPayslips.Add(new Payslip
            {
                PayrollItemId = payrollItemId,
                SlipNo = $"PS-{runNo}-{payrollItemId}-{now:yyMMddHHmm}",
                IssuedAt = now,
                IssuedBy = issuedBy,
                FileUrl = string.Empty
            });
        }

        if (newPayslips.Count > 0)
        {
            await _store.AddPayslipsAsync(newPayslips, cancellationToken);
        }

        return newPayslips.Count;
    }

    private async Task<int> MarkCommissionTransactionsProcessedAsync(
        IReadOnlyCollection<long> commissionTransactionIds,
        long userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var ids = commissionTransactionIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        var rows = await _store.GetCommissionTransactionsByIdsForUpdateAsync(ids, cancellationToken);

        var changed = 0;
        foreach (var row in rows)
        {
            var status = NormalizeStatus(row.Status, CommissionStatusPending);
            if (status != CommissionStatusPending && status != CommissionStatusApproved)
            {
                continue;
            }

            row.Status = CommissionStatusProcessed;
            row.UpdatedAt = now;
            row.UpdatedBy = userId;
            changed++;
        }

        if (changed > 0)
        {
            await _store.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    private sealed record PayrollRunResolution(PayrollRun Run, bool CreatedNew);
}
