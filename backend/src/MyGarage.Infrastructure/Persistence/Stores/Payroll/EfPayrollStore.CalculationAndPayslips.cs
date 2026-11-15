using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Common;
using MyGarage.Application.Payroll;
using MyGarage.Application.Payroll.DTOs;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Payroll;

public sealed partial class EfPayrollStore
{
    public async Task<IReadOnlyList<PayslipDto>> GetMyPayslipsAsync(long staffId, string? normalizedPeriod, CancellationToken ct = default)
    {
        var dbQuery = _context.Payslips
            .AsNoTracking()
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.PayrollRun)
                    .ThenInclude(pr => pr.PayrollPeriod)
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.Staff)
            .Where(p => p.PayrollItem.StaffId == staffId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedPeriod))
        {
            dbQuery = dbQuery.Where(p => p.PayrollItem.PayrollRun.PayrollPeriod.PeriodCode.ToUpper() == normalizedPeriod);
        }

        return await dbQuery
            .OrderByDescending(p => p.IssuedAt)
            .Select(p => new PayslipDto(
                p.Id,
                p.PayrollItemId,
                p.SlipNo,
                p.PayrollItem.StaffId,
                p.PayrollItem.Staff.FullName,
                p.PayrollItem.PayrollRun.PayrollPeriod.PeriodCode,
                p.PayrollItem.BaseSalary,
                p.PayrollItem.AllowanceAmount,
                p.PayrollItem.CommissionAmount,
                p.PayrollItem.OvertimeAmount,
                p.PayrollItem.BonusAmount,
                p.PayrollItem.AttendanceDeduction
                    + p.PayrollItem.AdvanceDeduction
                    + p.PayrollItem.LoanDeduction
                    + p.PayrollItem.InsuranceEmployeeAmount
                    + p.PayrollItem.PitAmount
                    + p.PayrollItem.OtherDeduction
                    + p.PayrollItem.PenaltyAmount,
                p.PayrollItem.NetPay,
                p.IssuedAt,
                p.ViewedAt
            ))
            .ToListAsync(ct);
    }

    public Task<long?> GetPayrollRunBranchIdAsync(long payrollRunId, CancellationToken ct = default)
    {
        return _context.PayrollRuns
            .AsNoTracking()
            .Where(r => r.Id == payrollRunId)
            .Select(r => (long?)r.BranchId)
            .FirstOrDefaultAsync(ct);
    }

    public Task<PayrollPeriod?> GetPayrollPeriodByIdAsync(long id, CancellationToken ct = default)
    {
        return _context.PayrollPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<PayrollPeriod?> GetLatestOpenPayrollPeriodAsync(CancellationToken ct = default)
    {
        var openStatus = nameof(PayrollPeriodStatusCode.OPEN);
        return _context.PayrollPeriods
            .AsNoTracking()
            .Where(x => x.Status != null && x.Status.ToUpper() == openStatus)
            .OrderByDescending(x => x.StartDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<PayrollRun?> GetLatestDraftPayrollRunAsync(long payrollPeriodId, long? branchId, CancellationToken ct = default)
    {
        var draftStatus = nameof(PayrollRunStatusCode.DRAFT);
        var query = _context.PayrollRuns
            .Where(x => x.PayrollPeriodId == payrollPeriodId && x.Status != null && x.Status.ToUpper() == draftStatus);
        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        return query
            .OrderByDescending(x => x.RunNo)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetMaxRunNoForPeriodAsync(long payrollPeriodId, long? branchId, CancellationToken ct = default)
    {
        var query = _context.PayrollRuns.Where(x => x.PayrollPeriodId == payrollPeriodId);
        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        return await query.Select(x => (int?)x.RunNo).MaxAsync(ct) ?? 0;
    }

    public async Task<IReadOnlyList<StaffPayrollSnapshot>> GetPayrollStaffSnapshotsAsync(long? branchId, CancellationToken ct = default)
    {
        var staffQuery = _context.Staffs
            .AsNoTracking()
            .Where(s => s.IsDeleted != true);
        if (branchId.HasValue)
        {
            staffQuery = staffQuery.Where(s => s.BranchId == branchId.Value);
        }

        return await staffQuery
            .Select(s => new StaffPayrollSnapshot
            {
                StaffId = s.Id,
                BranchId = s.BranchId,
                StaffName = s.FullName,
                HireDate = s.HireDate,
                TerminationDate = s.TerminationDate,
                Status = s.Status
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SalaryCoefficientThreshold>> GetActiveSalaryCoefficientThresholdsAsync(CancellationToken ct = default)
    {
        return await _context.SalaryCoefficients
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && x.IsActive != false && x.YearsOfService.HasValue)
            .OrderBy(x => x.YearsOfService)
            .Select(x => new SalaryCoefficientThreshold(x.YearsOfService!.Value, x.CoefficientValue))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StaffSalaryProfile>> GetSalaryProfilesAsOfAsync(
        IReadOnlyCollection<long> staffIds,
        long? branchId,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<StaffSalaryProfile>();

        var query = _context.StaffSalaryProfiles
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate));
        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value || !x.BranchId.HasValue);
        }

        return await query
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StaffContract>> GetContractsAsOfAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<StaffContract>();

        return await _context.StaffContracts
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<long, decimal>> GetStaffTierBaseSalariesAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default)
    {
        var result = new Dictionary<long, decimal>();
        if (staffIds.Count == 0) return result;

        var staffIdSet = staffIds.Where(id => id > 0).ToHashSet();
        if (staffIdSet.Count == 0) return result;

        var rows = await (
            from staff in _context.Staffs.AsNoTracking()
            join tier in _context.StaffTiers.AsNoTracking() on staff.StaffTierId equals tier.Id
            where staffIdSet.Contains(staff.Id)
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

    public async Task<IReadOnlyList<OvertimeEntry>> GetOvertimeEntriesInRangeAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<OvertimeEntry>();

        return await _context.OvertimeEntries
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= periodStartDate &&
                x.WorkDate <= periodEndDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttendanceAdjustment>> GetAttendanceAdjustmentsInRangeAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<AttendanceAdjustment>();

        return await _context.AttendanceAdjustments
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= periodStartDate &&
                x.WorkDate <= periodEndDate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SalaryAdvance>> GetSalaryAdvancesForPeriodAsync(
        IReadOnlyCollection<long> staffIds,
        long payrollPeriodId,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<SalaryAdvance>();

        return await _context.SalaryAdvances
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.DeductFromPayrollPeriodId == payrollPeriodId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmployeeLoan>> GetActiveLoansForPeriodAsync(
        IReadOnlyCollection<long> staffIds,
        long payrollPeriodId,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<EmployeeLoan>();

        return await _context.EmployeeLoans
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                (!x.StartPayrollPeriodId.HasValue || x.StartPayrollPeriodId.Value <= payrollPeriodId) &&
                x.OutstandingAmount > 0m)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PitProfile>> GetPitProfilesAsOfAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<PitProfile>();

        return await _context.PitProfiles
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EmployeeDependent>> GetEmployeeDependentsAsOfAsync(
        IReadOnlyCollection<long> staffIds,
        DateOnly asOfDate,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<EmployeeDependent>();

        return await _context.EmployeeDependents
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CommissionTransaction>> GetPositiveCommissionTransactionsAsync(
        IReadOnlyCollection<long> staffIds,
        CancellationToken ct = default)
    {
        if (staffIds.Count == 0) return Array.Empty<CommissionTransaction>();

        return await _context.CommissionTransactions
            .AsNoTracking()
            .Where(x => staffIds.Contains(x.StaffId) && x.CommissionAmount > 0m)
            .ToListAsync(ct);
    }

    public Task<List<PayrollItem>> GetPayrollItemsByRunIdAsync(long payrollRunId, CancellationToken ct = default)
    {
        return _context.PayrollItems
            .Where(i => i.PayrollRunId == payrollRunId)
            .ToListAsync(ct);
    }

    public void AddPayrollItem(PayrollItem item)
    {
        _context.PayrollItems.Add(item);
    }

    public async Task<DateOnly?> GetStaffHireDateFallbackAsync(long staffId, CancellationToken ct = default)
    {
        var staff = await _context.Staffs
            .AsNoTracking()
            .Where(x => x.Id == staffId)
            .Select(x => new { x.HireDate, x.CreatedAt })
            .FirstOrDefaultAsync(ct);
        if (staff is null) return null;
        if (staff.HireDate.HasValue) return staff.HireDate.Value;
        if (staff.CreatedAt.HasValue) return DateOnly.FromDateTime(staff.CreatedAt.Value);

        var contractHireDate = await _context.StaffContracts
            .AsNoTracking()
            .Where(x => x.StaffId == staffId)
            .OrderBy(x => x.EffectiveFrom)
            .Select(x => (DateOnly?)x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
        if (contractHireDate.HasValue) return contractHireDate.Value;

        return await _context.StaffSalaryProfiles
            .AsNoTracking()
            .Where(x => x.StaffId == staffId)
            .OrderBy(x => x.EffectiveFrom)
            .Select(x => (DateOnly?)x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
    }

    public Task<List<CommissionTransaction>> GetCommissionTransactionsByIdsForUpdateAsync(
        IReadOnlyCollection<long> ids,
        CancellationToken ct = default)
    {
        if (ids.Count == 0) return Task.FromResult(new List<CommissionTransaction>());

        return _context.CommissionTransactions
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(ct);
    }

    public async Task<HashSet<long>> GetExistingPayslipItemIdsAsync(
        IReadOnlyCollection<long> payrollItemIds,
        CancellationToken ct = default)
    {
        if (payrollItemIds.Count == 0) return new HashSet<long>();

        var existingIds = await _context.Payslips
            .Where(p => payrollItemIds.Contains(p.PayrollItemId))
            .Select(p => p.PayrollItemId)
            .ToListAsync(ct);
        return existingIds.ToHashSet();
    }

    public async Task AddPayslipsAsync(IEnumerable<Payslip> payslips, CancellationToken ct = default)
    {
        _context.Payslips.AddRange(payslips);
        await _context.SaveChangesAsync(ct);
    }
}
