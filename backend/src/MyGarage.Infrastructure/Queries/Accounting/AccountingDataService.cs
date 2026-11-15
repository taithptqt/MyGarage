using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfAccountingDataService(IGarageContext db) : IAccountingDataService
{
    private const string PayrollRunStatusDraft = nameof(PayrollRunStatusCode.DRAFT);
    private const string PayrollRunStatusCalculated = nameof(PayrollRunStatusCode.CALCULATED);

    public async Task<(bool Found, string? ErrorMessage, long PayrollPeriodId)> CancelPayrollRunAsync(
        long id,
        long? accessBranchId,
        long actorUserId,
        CancellationToken ct = default)
    {
        var run = await db.PayrollRuns
            .Include(r => r.PayrollPeriod)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
            return (false, null, 0);

        if (accessBranchId.HasValue && run.BranchId != accessBranchId.Value)
            return (false, null, 0);

        var currentStatus = (run.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (currentStatus is not PayrollRunStatusDraft and not PayrollRunStatusCalculated)
            return (true, $"Only DRAFT or CALCULATED payroll run can be canceled (current: {run.Status}).", 0);

        run.Status = nameof(PayrollRunStatusCode.CANCELED);
        run.UpdatedAt = VietnamTime.Now;

        var staffIdsInRun = await db.PayrollItems
            .AsNoTracking()
            .Where(i => i.PayrollRunId == run.Id)
            .Select(i => i.StaffId)
            .Distinct()
            .ToListAsync(ct);

        if (staffIdsInRun.Count > 0 && run.PayrollPeriod is not null)
        {
            var periodStart = run.PayrollPeriod.StartDate.ToDateTime(TimeOnly.MinValue);
            var periodEndExclusive = (run.PayrollPeriod.EndDate != default ? run.PayrollPeriod.EndDate : run.PayrollPeriod.PayrollDate)
                .AddDays(1)
                .ToDateTime(TimeOnly.MinValue);

            var activeOtherStaffIds = await db.PayrollItems
                .AsNoTracking()
                .Where(i =>
                    i.PayrollRunId != run.Id &&
                    i.PayrollRun.PayrollPeriodId == run.PayrollPeriodId &&
                    i.PayrollRun.Status != nameof(PayrollRunStatusCode.CANCELED) &&
                    staffIdsInRun.Contains(i.StaffId))
                .Select(i => i.StaffId)
                .Distinct()
                .ToListAsync(ct);

            var revertStaffIds = staffIdsInRun.Except(activeOtherStaffIds).ToHashSet();
            if (revertStaffIds.Count > 0)
            {
                var processedCommissions = await db.CommissionTransactions
                    .Where(comm =>
                        revertStaffIds.Contains(comm.StaffId) &&
                        comm.Status == "PROCESSED" &&
                        (comm.CreatedAt ?? comm.ApprovedAt) >= periodStart &&
                        (comm.CreatedAt ?? comm.ApprovedAt) < periodEndExclusive)
                    .ToListAsync(ct);

                foreach (var comm in processedCommissions)
                {
                    comm.Status = nameof(CommissionTransactionStatusCode.APPROVED);
                    comm.UpdatedBy = actorUserId;
                    comm.UpdatedAt = VietnamTime.Now;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return (true, null, run.PayrollPeriodId);
    }

    public async Task<(bool Found, string? Status, int RunNo)> GetPayrollRunStatusAsync(
        long id,
        long? accessBranchId,
        CancellationToken ct = default)
    {
        var run = await db.PayrollRuns
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.BranchId, x.Status, x.RunNo })
            .FirstOrDefaultAsync(ct);

        if (run is null)
            return (false, null, 0);
        if (accessBranchId.HasValue && run.BranchId.HasValue && run.BranchId.Value != accessBranchId.Value)
            return (false, null, 0);

        return (true, run.Status, run.RunNo);
    }

    public Task<PayrollItem?> GetPayrollItemWithRunAsync(long payrollItemId, CancellationToken ct = default) =>
        db.PayrollItems
            .AsNoTracking()
            .Include(x => x.PayrollRun)
            .FirstOrDefaultAsync(x => x.Id == payrollItemId, ct);

    public Task<Payslip?> GetPayslipByPayrollItemIdAsync(long payrollItemId, CancellationToken ct = default) =>
        db.Payslips
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PayrollItemId == payrollItemId, ct);

    public Task<PayrollRun?> GetPayrollRunWithPeriodAsync(long payrollRunId, CancellationToken ct = default) =>
        db.PayrollRuns
            .AsNoTracking()
            .Include(x => x.PayrollPeriod)
            .FirstOrDefaultAsync(x => x.Id == payrollRunId, ct);

    public async Task<List<(long StaffId, decimal CommissionAmount)>> GetPayrollItemCommissionsAsync(
        long payrollRunId,
        CancellationToken ct = default)
    {
        var rows = await db.PayrollItems
            .AsNoTracking()
            .Where(x => x.PayrollRunId == payrollRunId)
            .Select(x => new { x.StaffId, x.CommissionAmount })
            .ToListAsync(ct);

        return rows.Select(x => (x.StaffId, x.CommissionAmount)).ToList();
    }

    public Task<List<CommissionTransaction>> GetPositiveCommissionTransactionsAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default) =>
        db.CommissionTransactions
            .AsNoTracking()
            .Where(x => x.CommissionAmount > 0m)
            .Where(x => !staffIds.Any() || staffIds.Contains(x.StaffId))
            .ToListAsync(ct);

    public Task<Staff?> GetActiveStaffAsync(long staffId, CancellationToken ct = default) =>
        db.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true, ct);

    public async Task<Dictionary<long, decimal>> LoadStaffTierBaseSalariesAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var rows = await (
            from staff in db.Staffs.AsNoTracking()
            join tier in db.StaffTiers.AsNoTracking() on staff.StaffTierId equals tier.Id
            where staffIds.Contains(staff.Id)
                  && staff.IsDeleted != true
                  && tier.IsDeleted != true
                  && tier.BaseSalary.HasValue
                  && tier.BaseSalary.Value > 0m
            select new
            {
                staff.Id,
                BaseSalary = tier.BaseSalary!.Value
            }).ToListAsync(ct);

        foreach (var row in rows)
        {
            result[row.Id] = row.BaseSalary;
        }

        return result;
    }

    public Task<decimal?> GetSalaryCoefficientAsync(int yearsOfService, CancellationToken ct = default) =>
        db.SalaryCoefficients
            .AsNoTracking()
            .Where(item =>
                item.IsDeleted != true &&
                item.IsActive != false &&
                item.YearsOfService.HasValue &&
                item.YearsOfService.Value <= yearsOfService)
            .OrderByDescending(item => item.YearsOfService)
            .Select(item => (decimal?)item.CoefficientValue)
            .FirstOrDefaultAsync(ct);
}
