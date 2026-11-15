using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api/payslips")]
[Route("api/v1/payslips")]
[ApiController]
[Authorize]
public sealed class PayslipsController : ControllerBase
{
    private readonly IGarageContext _context;

    public PayslipsController(IGarageContext context)
    {
        _context = context;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyPayslips([FromQuery] string? period = null)
    {
        if (!this.IsStaffUser())
        {
            return Forbid();
        }

        var staffId = this.GetCurrentUserId();
        if (!staffId.HasValue || staffId.Value <= 0)
        {
            return Unauthorized(new { message = "Không xác định được nhân viên hiện tại." });
        }

        var query = _context.Payslips
            .AsNoTracking()
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.PayrollRun)
                    .ThenInclude(pr => pr.PayrollPeriod)
            .Include(p => p.PayrollItem)
                .ThenInclude(pi => pi.Staff)
            .Where(p => p.PayrollItem.StaffId == staffId.Value);

        if (!string.IsNullOrWhiteSpace(period))
        {
            var normalizedPeriod = period.Trim().ToUpperInvariant();
            query = query.Where(p => p.PayrollItem.PayrollRun.PayrollPeriod.PeriodCode.ToUpper() == normalizedPeriod);
        }

        var rows = await query
            .OrderByDescending(p => p.IssuedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync();

        var items = rows.Select(p =>
        {
            var item = p.PayrollItem;
            var run = item.PayrollRun;
            var deductions = item.AttendanceDeduction
                + item.AdvanceDeduction
                + item.LoanDeduction
                + item.InsuranceEmployeeAmount
                + item.PitAmount
                + item.OtherDeduction;

            return new
            {
                id = p.Id,
                payrollItemId = p.PayrollItemId,
                payrollRunId = run.Id,
                runNo = run.RunNo,
                slipNo = p.SlipNo,
                period = run.PayrollPeriod.PeriodCode,
                startDate = run.PayrollPeriod.StartDate,
                endDate = run.PayrollPeriod.EndDate,
                payrollDate = run.PayrollPeriod.PayrollDate,
                status = run.Status,
                staffId = item.StaffId,
                staffName = item.Staff?.FullName,
                baseSalary = item.BaseSalary,
                commissionAmount = item.CommissionAmount,
                overtimeAmount = item.OvertimeAmount,
                allowanceAmount = item.AllowanceAmount,
                bonus = item.BonusAmount,
                deductions,
                attendanceDeduction = item.AttendanceDeduction,
                advanceDeduction = item.AdvanceDeduction,
                loanDeduction = item.LoanDeduction,
                insuranceEmployeeAmount = item.InsuranceEmployeeAmount,
                pitAmount = item.PitAmount,
                otherDeduction = item.OtherDeduction,
                penalty = item.PenaltyAmount,
                netSalary = item.NetPay,
                netPay = item.NetPay,
                issuedAt = p.IssuedAt,
                viewedAt = p.ViewedAt,
                fileUrl = p.FileUrl
            };
        }).ToList();

        return Ok(new { items });
    }
}
