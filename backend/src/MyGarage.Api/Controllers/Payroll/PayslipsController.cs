using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers;

[Route("api/v1/payslips")]
[ApiController]
[Authorize]
public sealed class PayslipsController : ControllerBase
{
    private readonly IPayslipQueryService _payslipQueryService;

    public PayslipsController(IPayslipQueryService payslipQueryService)
    {
        _payslipQueryService = payslipQueryService;
    }

    [HttpGet("my")]
    [HttpGet("~/api/v1/staff/current/payslips")]
    public async Task<IActionResult> GetMyPayslips([FromQuery] string? period = null)
    {
        if (!this.IsStaffUser())
        {
            return Forbid();
        }

        var staffId = this.GetCurrentUserId();
        if (!staffId.HasValue || staffId.Value <= 0)
        {
            return this.UnauthorizedProblem("Không xác định được nhân viên hiện tại.");
        }

        var rows = await _payslipQueryService.GetMyPayslipsAsync(staffId.Value, period);

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
