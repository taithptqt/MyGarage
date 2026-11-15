using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Common.Pagination;
using MyGarage.Enums;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Services;

namespace MyGarage.Controllers;

[Route("api/payroll-runs")]
[Route("api/v1/payroll-runs")]
[ApiController]
[Authorize]
public sealed class PayrollRunsController : ControllerBase
{
    public sealed class PayrollRunCreateItemDto
    {
        public long StaffId { get; set; }
        public decimal? BaseSalary { get; set; }
        public decimal? CommissionAmount { get; set; }
        public decimal? OvertimeAmount { get; set; }
        public decimal? AllowanceAmount { get; set; }
        public decimal? Bonus { get; set; }
        public decimal? BonusAmount { get; set; }
        public decimal? Deductions { get; set; }
        public decimal? Penalty { get; set; }
        public decimal? PenaltyAmount { get; set; }
    }

    public sealed class PayrollRunCreateEndpointDto
    {
        public string? Period { get; set; }
        public long? BranchId { get; set; }
        public long? CreatedBy { get; set; }
        public List<PayrollRunCreateItemDto>? Details { get; set; }
    }

    private readonly IGarageContext _context;
    private readonly IPayrollBatchCalculationService _batchCalculationService;

    public PayrollRunsController(
        IGarageContext context,
        IPayrollBatchCalculationService batchCalculationService)
    {
        _context = context;
        _batchCalculationService = batchCalculationService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? branchId = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null)
    {
        if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
        {
            return denied!;
        }

        if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER", "NHAN_SU"))
        {
            return Forbid();
        }

        var query = _context.PayrollRuns
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

        var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
        var total = await query.CountAsync();
        var offset = ((long)normalizedPage - 1) * normalizedSize;
        var pagedRuns = offset >= total
            ? new List<PayrollRun>()
            : await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.Id)
                .Skip((int)offset)
                .Take(normalizedSize)
                .ToListAsync();
        var items = pagedRuns.Select(MapPayrollRunPayload).ToList();
        return Ok(PagedEnvelope<object>.Create(items, total, normalizedPage, normalizedSize));
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(
        [FromQuery] long? branchId = null,
        [FromQuery] string? period = null)
    {
        if (!this.TryResolveRequiredBranchId(branchId, out var targetBranchId, out var denied))
        {
            return denied!;
        }

        if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER", "NHAN_SU"))
        {
            return Forbid();
        }

        if (!TryParsePeriod(period, out var periodCode, out var startDate, out var endDate))
        {
            return BadRequest(new { message = "Định dạng period phải là yyyy-MM." });
        }

        var asOfDate = endDate;
        var periodStartDt = startDate.ToDateTime(TimeOnly.MinValue);
        var periodEndExclusiveDt = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var staffs = await _context.Staffs
            .AsNoTracking()
            .Include(s => s.StaffTier)
            .Where(s =>
                s.IsDeleted != true &&
                s.BranchId == targetBranchId &&
                (s.Status == null || s.Status == nameof(StaffStatusCode.ACTIVE) || s.Status == nameof(StaffStatusCode.ON_LEAVE)))
            .OrderBy(s => s.FullName)
            .ToListAsync();

        var staffIds = staffs.Select(s => s.Id).ToList();
        if (staffIds.Count == 0)
        {
            return Ok(new
            {
                period = periodCode,
                branchId = targetBranchId,
                status = nameof(PayrollRunStatusCode.DRAFT),
                totalAmount = 0m,
                details = Array.Empty<object>()
            });
        }

        var profiles = await _context.StaffSalaryProfiles
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        var contracts = await _context.StaffContracts
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.EffectiveFrom <= asOfDate &&
                (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= asOfDate))
            .OrderByDescending(x => x.EffectiveFrom)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

        var overtimeByStaff = await _context.OvertimeEntries
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= startDate &&
                x.WorkDate <= endDate &&
                (x.Status == "APPROVED" || x.Status == "APPLIED" || x.Status == "PAID"))
            .GroupBy(x => x.StaffId)
            .Select(g => new { StaffId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.StaffId, x => x.Amount);

        var commissionsByStaff = await _context.CommissionTransactions
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                (x.Status == nameof(CommissionTransactionStatusCode.APPROVED) || x.Status == "PROCESSED") &&
                (x.CreatedAt ?? x.ApprovedAt) >= periodStartDt &&
                (x.CreatedAt ?? x.ApprovedAt) < periodEndExclusiveDt)
            .GroupBy(x => x.StaffId)
            .Select(g => new { StaffId = g.Key, Amount = g.Sum(x => x.CommissionAmount) })
            .ToDictionaryAsync(x => x.StaffId, x => x.Amount);

        var attendanceAdjustments = await _context.AttendanceAdjustments
            .AsNoTracking()
            .Where(x =>
                staffIds.Contains(x.StaffId) &&
                x.WorkDate >= startDate &&
                x.WorkDate <= endDate &&
                (x.Status == "APPROVED" || x.Status == "APPLIED"))
            .ToListAsync();

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

            return new
            {
                staffId = s.Id,
                staffName = s.FullName,
                baseSalary,
                allowanceAmount,
                commissionAmount,
                overtimeAmount,
                bonus,
                deductions,
                penalty,
                netSalary
            };
        }).ToList();

        return Ok(new
        {
            period = periodCode,
            branchId = targetBranchId,
            status = nameof(PayrollRunStatusCode.DRAFT),
            totalAmount = details.Sum(x => x.netSalary),
            details
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PayrollRunCreateEndpointDto? payload)
    {
        if (!this.TryResolveRequiredBranchId(payload?.BranchId, out var targetBranchId, out var denied))
        {
            return denied!;
        }

        if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER", "NHAN_SU"))
        {
            return Forbid();
        }

        if (!TryParsePeriod(payload?.Period, out var periodCode, out var startDate, out var endDate))
        {
            return BadRequest(new { message = "Định dạng period phải là yyyy-MM." });
        }

        var now = VietnamTime.Now;
        var actorId = this.GetCurrentUserId() ?? payload?.CreatedBy;

        var periodEntity = await _context.PayrollPeriods
            .FirstOrDefaultAsync(p => p.PeriodCode == periodCode);

        if (periodEntity != null && !string.Equals(periodEntity.Status?.Trim(), nameof(PayrollPeriodStatusCode.OPEN), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Kỳ lương không ở trạng thái mở." });

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
            _context.PayrollPeriods.Add(periodEntity);
            await _context.SaveChangesAsync();
        }

        if (payload?.Details == null || payload.Details.Count == 0)
        {
            var batchResult = await _batchCalculationService.CalculatePayrollForAllAsync(
                new PayrollBatchCalculationCommand
                {
                    UserId = actorId ?? 0,
                    BranchId = targetBranchId,
                    PayrollPeriodId = periodEntity.Id,
                    OverwriteExisting = true,
                    IncludeInactiveStaff = false,
                    IssuePayslips = false
                },
                HttpContext.RequestAborted);

            var calculatedRun = await _context.PayrollRuns
                .AsNoTracking()
                .Include(r => r.PayrollPeriod)
                .Include(r => r.PayrollItems)
                    .ThenInclude(i => i.Staff)
                .FirstAsync(r => r.Id == batchResult.PayrollRunId);

            return Ok(MapPayrollRunPayload(calculatedRun));
        }

        var nextRunNo = (await _context.PayrollRuns
            .AsNoTracking()
            .Where(r => r.PayrollPeriodId == periodEntity.Id && r.BranchId == targetBranchId)
            .MaxAsync(r => (int?)r.RunNo) ?? 0) + 1;

        var requestedStaffIds = payload.Details.Select(d => d.StaffId).Where(id => id > 0).Distinct().ToList();
        var validStaffIds = await _context.Staffs
            .AsNoTracking()
            .Where(s => requestedStaffIds.Contains(s.Id) && s.BranchId == targetBranchId && s.IsDeleted != true)
            .Select(s => s.Id)
            .ToListAsync();
        var validStaffSet = validStaffIds.ToHashSet();

        if (validStaffSet.Count == 0)
        {
            return BadRequest(new { message = "Không có nhân viên hợp lệ thuộc chi nhánh này trong bảng lương." });
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

        foreach (var line in payload.Details.Where(d => d != null && validStaffSet.Contains(d.StaffId)))
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

        _context.PayrollRuns.Add(run);
        await _context.SaveChangesAsync();

        var createdRun = await _context.PayrollRuns
            .AsNoTracking()
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .FirstAsync(r => r.Id == run.Id);

        return CreatedAtAction(nameof(GetAll), new { branchId = targetBranchId }, MapPayrollRunPayload(createdRun));
    }

    [HttpPut("{id:long}/approve")]
    public async Task<IActionResult> Approve(long id)
    {
        if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
        {
            return denied!;
        }

        if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER", "NHAN_SU"))
        {
            return Forbid();
        }

        var run = await _context.PayrollRuns
            .Include(r => r.PayrollPeriod)
            .Include(r => r.PayrollItems)
                .ThenInclude(i => i.Staff)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (run == null)
        {
            return NotFound(new { message = "Không tìm thấy bảng lương." });
        }

        if (scopedBranchId.HasValue && run.BranchId != scopedBranchId.Value)
        {
            return Forbid();
        }

        var currentStatus = (run.Status ?? string.Empty).Trim().ToUpperInvariant();
        if (currentStatus is not nameof(PayrollRunStatusCode.DRAFT) and not nameof(PayrollRunStatusCode.CALCULATED))
        {
            return BadRequest(new { message = $"Chỉ có thể duyệt bảng lương ở trạng thái DRAFT hoặc CALCULATED (hiện tại: {run.Status})." });
        }

        if (run.PayrollItems.Count == 0)
        {
            return BadRequest(new { message = "Bảng lương không có dòng chi tiết nhân viên." });
        }

        var now = VietnamTime.Now;
        var actorId = this.GetCurrentUserId();
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

        await _context.SaveChangesAsync();
        return Ok(MapPayrollRunPayload(run));
    }

    private static bool TryParsePeriod(string? period, out string normalizedCode, out DateOnly startDate, out DateOnly endDate)
    {
        normalizedCode = string.Empty;
        startDate = default;
        endDate = default;

        if (string.IsNullOrWhiteSpace(period))
        {
            var now = VietnamTime.Now;
            normalizedCode = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            startDate = new DateOnly(now.Year, now.Month, 1);
            endDate = startDate.AddMonths(1).AddDays(-1);
            return true;
        }

        if (!DateOnly.TryParseExact(
                $"{period.Trim()}-01",
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var firstDay))
        {
            return false;
        }

        normalizedCode = firstDay.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        startDate = firstDay;
        endDate = firstDay.AddMonths(1).AddDays(-1);
        return true;
    }

    private static object MapPayrollRunPayload(PayrollRun run)
    {
        var details = run.PayrollItems.Select(i =>
        {
            var deductions = i.AttendanceDeduction
                + i.AdvanceDeduction
                + i.LoanDeduction
                + i.InsuranceEmployeeAmount
                + i.PitAmount
                + i.OtherDeduction;

            return new
            {
                id = i.Id,
                staffId = i.StaffId,
                staffName = i.Staff?.FullName,
                baseSalary = i.BaseSalary,
                allowanceAmount = i.AllowanceAmount,
                commissionAmount = i.CommissionAmount,
                overtimeAmount = i.OvertimeAmount,
                bonus = i.BonusAmount,
                deductions,
                penalty = i.PenaltyAmount,
                netSalary = i.NetPay,
                netPay = i.NetPay,
                status = i.Status
            };
        }).ToList();

        return new
        {
            id = run.Id,
            payrollPeriodId = run.PayrollPeriodId,
            period = run.PayrollPeriod?.PeriodCode,
            runNo = run.RunNo,
            branchId = run.BranchId,
            status = run.Status,
            calculatedAt = run.CalculatedAt,
            approvedAt = run.ApprovedAt,
            paidAt = run.PaidAt,
            createdAt = run.CreatedAt,
            totalAmount = details.Sum(d => d.netSalary),
            details
        };
    }
}
