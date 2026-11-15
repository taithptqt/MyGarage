using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfPayrollRunDataService(
    IGarageContext context,
    IPayrollBatchCalculationService batchCalculationService) : IPayrollRunDataService
{
    public async Task<(IReadOnlyList<PayrollRun> Items, int Total)> GetPagedAsync(
        long? scopedBranchId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .Where(r => r.Status != nameof(PayrollRunStatusCode.CANCELED))
            .AsQueryable();

        if (scopedBranchId.HasValue)
        {
            query = query.Where(r => r.BranchId == scopedBranchId.Value);
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var pagedRuns = offset >= total
            ? new List<PayrollRun>()
            : await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(ct);

        return (pagedRuns, total);
    }

    public async Task<PayrollRunPreviewResult> PreviewAsync(
        long targetBranchId,
        string periodCode,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        var asOfDate = endDate;
        var periodStartDt = startDate.ToDateTime(TimeOnly.MinValue);
        var periodEndExclusiveDt = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var staffs = await context.Staffs
            .AsNoTracking()
            .Include(s => s.StaffTier)
            .Where(s =>
                s.IsDeleted != true &&
                s.BranchId == targetBranchId &&
                (s.Status == null || s.Status == nameof(StaffStatusCode.ACTIVE) || s.Status == nameof(StaffStatusCode.ON_LEAVE)))
            .OrderBy(s => s.FullName)
            .ToListAsync(ct);

        var staffIds = staffs.Select(s => s.Id).ToList();
        if (staffIds.Count == 0)
        {
            return new PayrollRunPreviewResult(
                periodCode,
                targetBranchId,
                nameof(PayrollRunStatusCode.DRAFT),
                0m,
                Array.Empty<PayrollRunPreviewDetail>());
        }

        var profiles = await context.StaffSalaryProfiles
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var contracts = await context.StaffContracts
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var overtimeByStaff = await context.OvertimeEntries
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= startDate &&
                x.WorkDate <= endDate &&
                (x.Status == "APPROVED" || x.Status == "APPLIED" || x.Status == "PAID"))
            .GroupBy(x => x.StaffId)
            .Select(g => new { StaffId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.StaffId, x => x.Amount, ct);

        var commissionsByStaff = await context.CommissionTransactions
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                (x.Status == nameof(CommissionTransactionStatusCode.APPROVED) || x.Status == "PROCESSED") &&
                (x.CreatedAt ?? x.ApprovedAt) >= periodStartDt &&
                (x.CreatedAt ?? x.ApprovedAt) < periodEndExclusiveDt)
            .GroupBy(x => x.StaffId)
            .Select(g => new { StaffId = g.Key, Amount = g.Sum(x => x.CommissionAmount) })
            .ToDictionaryAsync(x => x.StaffId, x => x.Amount, ct);

        var attendanceAdjustments = await context.AttendanceAdjustments
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= startDate &&
                x.WorkDate <= endDate &&
                (x.Status == "APPROVED" || x.Status == "APPLIED"))
            .ToListAsync(ct);

        var deductionsByStaff = attendanceAdjustments
            .GroupBy(x => x.StaffId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.AmountImpact < 0m ? Math.Abs(x.AmountImpact) : 0m));

        var details = staffs.Select(s =>
        {
            var activeProfile = profiles.FirstOrDefault(p =>
                p.StaffId == s.Id &&
                (string.IsNullOrWhiteSpace(p.Status) || string.Equals(p.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)));
            var activeContract = contracts.FirstOrDefault(c =>
                c.StaffId == s.Id &&
                (string.IsNullOrWhiteSpace(c.Status) || string.Equals(c.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)));

            var baseSalary = activeProfile?.BaseSalary > 0m
                ? activeProfile.BaseSalary
                : (activeContract?.BaseSalary > 0m ? activeContract.BaseSalary : (s.StaffTier?.BaseSalary ?? 0m));
            var allowanceAmount = activeContract?.AllowanceAmount ?? 0m;
            var commissionAmount = commissionsByStaff.TryGetValue(s.Id, out var cAmt) ? cAmt : 0m;
            var overtimeAmount = overtimeByStaff.TryGetValue(s.Id, out var oAmt) ? oAmt : 0m;
            var deductions = deductionsByStaff.TryGetValue(s.Id, out var dAmt) ? dAmt : 0m;
            var bonus = 0m;
            var penalty = 0m;
            var netSalary = baseSalary + allowanceAmount + commissionAmount + overtimeAmount + bonus - deductions - penalty;

            return new PayrollRunPreviewDetail(
                s.Id,
                s.FullName,
                baseSalary,
                allowanceAmount,
                commissionAmount,
                overtimeAmount,
                bonus,
                deductions,
                penalty,
                netSalary);
        }).ToList();

        return new PayrollRunPreviewResult(
            periodCode,
            targetBranchId,
            nameof(PayrollRunStatusCode.DRAFT),
            details.Sum(x => x.NetSalary),
            details);
    }

    public async Task<PayrollRunCreateResult> CreateAsync(
        long targetBranchId,
        string periodCode,
        DateOnly startDate,
        DateOnly endDate,
        long? actorId,
        IReadOnlyList<PayrollRunCreateLineInput>? details,
        CancellationToken ct = default)
    {
        var now = VietnamTime.Now;
        var periodEntity = await context.PayrollPeriods
            .FirstOrDefaultAsync(p => p.PeriodCode == periodCode, ct);

        if (periodEntity != null && !string.Equals(periodEntity.Status?.Trim(), nameof(PayrollPeriodStatusCode.OPEN), StringComparison.OrdinalIgnoreCase))
        {
            return new PayrollRunCreateResult(PayrollRunCreateStatus.BadRequest, "Kỳ lương không ở trạng thái mở.");
        }

        if (periodEntity == null)
        {
            periodEntity = new PayrollPeriod
            {
                PeriodCode = periodCode,
                StartDate = startDate,
                EndDate = endDate,
                PayrollDate = endDate,
                Status = nameof(PayrollPeriodStatusCode.OPEN),
                CreatedAt = now,
                UpdatedAt = now
            };
            context.PayrollPeriods.Add(periodEntity);
            await context.SaveChangesAsync(ct);
        }

        if (details == null || details.Count == 0)
        {
            var batchResult = await batchCalculationService.CalculatePayrollForAllAsync(
                new PayrollBatchCalculationCommand
                {
                    UserId = actorId ?? 0,
                    BranchId = targetBranchId,
                    PayrollPeriodId = periodEntity.Id,
                    OverwriteExisting = true,
                    IncludeInactiveStaff = false,
                    IssuePayslips = false
                },
                ct);

            var calculatedRun = await context.PayrollRuns
                .AsNoTracking()
                .Include(r => r.PayrollPeriod)
                .Include(r => r.PayrollItems)
                    .ThenInclude(i => i.Staff)
                .FirstAsync(r => r.Id == batchResult.PayrollRunId, ct);

            return new PayrollRunCreateResult(PayrollRunCreateStatus.Ok, Run: calculatedRun);
        }

        var nextRunNo = (await context.PayrollRuns
            .AsNoTracking()
            .Where(r => r.PayrollPeriodId == periodEntity.Id && r.BranchId == targetBranchId)
            .MaxAsync(r => (int?)r.RunNo, ct) ?? 0) + 1;

        var requestedStaffIds = details.Select(d => d.StaffId).Where(id => id > 0).Distinct().ToList();
        var validStaffIds = await context.Staffs
            .AsNoTracking()
            .Where(s => requestedStaffIds.Contains(s.Id) && s.BranchId == targetBranchId && s.IsDeleted != true)
            .Select(s => s.Id)
            .ToListAsync(ct);
        var validStaffSet = validStaffIds.ToHashSet();

        if (validStaffSet.Count == 0)
        {
            return new PayrollRunCreateResult(
                PayrollRunCreateStatus.BadRequest,
                "Không có nhân viên hợp lệ thuộc chi nhánh này trong bảng lương.");
        }

        var run = new PayrollRun
        {
            PayrollPeriodId = periodEntity.Id,
            BranchId = targetBranchId,
            RunNo = nextRunNo,
            Status = nameof(PayrollRunStatusCode.CALCULATED),
            CalculatedBy = actorId,
            CalculatedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var line in details.Where(d => d != null && validStaffSet.Contains(d.StaffId)))
        {
            var baseSalary = Math.Max(0m, line.BaseSalary ?? 0m);
            var commission = line.CommissionAmount ?? 0m;
            var overtime = Math.Max(0m, line.OvertimeAmount ?? 0m);
            var allowance = Math.Max(0m, line.AllowanceAmount ?? 0m);
            var bonus = Math.Max(0m, line.BonusAmount ?? line.Bonus ?? 0m);
            var penalty = Math.Max(0m, line.PenaltyAmount ?? line.Penalty ?? 0m);
            var deductions = Math.Max(0m, line.Deductions ?? 0m);
            var netPay = baseSalary + allowance + commission + overtime + bonus - deductions - penalty;

            run.PayrollItems.Add(new PayrollItem
            {
                StaffId = line.StaffId,
                BaseSalary = baseSalary,
                AllowanceAmount = allowance,
                CommissionAmount = commission,
                OvertimeAmount = overtime,
                BonusAmount = bonus,
                OtherDeduction = deductions,
                PenaltyAmount = penalty,
                NetPay = netPay,
                Status = nameof(PayrollItemStatusCode.DRAFT),
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        context.PayrollRuns.Add(run);
        await context.SaveChangesAsync(ct);

        var createdRun = await context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .FirstAsync(r => r.Id == run.Id, ct);

        return new PayrollRunCreateResult(PayrollRunCreateStatus.Created, Run: createdRun);
    }

    public async Task<PayrollRunApproveResult> ApproveAsync(
        long id,
        long? scopedBranchId,
        long? actorId,
        CancellationToken ct = default)
    {
        var run = await context.PayrollRuns
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run == null)
        {
            return new PayrollRunApproveResult(PayrollRunApproveStatus.NotFound, "Không tìm thấy bảng lương.");
        }

        if (scopedBranchId.HasValue && run.BranchId != scopedBranchId.Value)
        {
            return new PayrollRunApproveResult(PayrollRunApproveStatus.Forbidden);
        }

        var currentStatus = (run.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (currentStatus is not nameof(PayrollRunStatusCode.DRAFT) and not nameof(PayrollRunStatusCode.CALCULATED))
        {
            return new PayrollRunApproveResult(
                PayrollRunApproveStatus.BadRequest,
                $"Chỉ có thể duyệt bảng lương ở trạng thái DRAFT hoặc CALCULATED (hiện tại: {run.Status}).");
        }

        if (run.PayrollItems.Count == 0)
        {
            return new PayrollRunApproveResult(
                PayrollRunApproveStatus.BadRequest,
                "Bảng lương không có dòng chi tiết nhân viên.");
        }

        var now = VietnamTime.Now;
        run.Status = nameof(PayrollRunStatusCode.APPROVED);
        run.ApprovedBy = actorId;
        run.ApprovedAt = now;
        run.UpdatedAt = now;

        foreach (var item in run.PayrollItems)
        {
            if (string.Equals(item.Status, nameof(PayrollItemStatusCode.DRAFT), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Status, nameof(PayrollRunStatusCode.CALCULATED), StringComparison.OrdinalIgnoreCase))
            {
                item.Status = nameof(PayrollItemStatusCode.APPROVED);
                item.UpdatedAt = now;
            }
        }

        await context.SaveChangesAsync(ct);
        return new PayrollRunApproveResult(PayrollRunApproveStatus.Ok, Run: run);
    }
}
