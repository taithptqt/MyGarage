using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payroll;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers;

[Route("api/v1/payroll-runs")]
[ApiController]
[Authorize]
public sealed class PayrollRunsController : ControllerBase
{
    private readonly IPayrollRunsApplicationService _payrollRunsAppService;

    public PayrollRunsController(IPayrollRunsApplicationService payrollRunsAppService)
    {
        _payrollRunsAppService = payrollRunsAppService;
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

        if (!IsPayrollAuthorizedStaff())
        {
            return Forbid();
        }

        var result = await _payrollRunsAppService.GetAllAsync(scopedBranchId, page, pageSize, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpGet("previews")]
    public async Task<IActionResult> Preview(
        [FromQuery] long? branchId = null,
        [FromQuery] string? period = null)
    {
        if (!this.TryResolveRequiredBranchId(branchId, out var targetBranchId, out var denied))
        {
            return denied!;
        }

        if (!IsPayrollAuthorizedStaff())
        {
            return Forbid();
        }

        var result = await _payrollRunsAppService.PreviewAsync(targetBranchId, period, HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PayrollRunCreateEndpointDto? payload)
    {
        if (!this.TryResolveRequiredBranchId(payload?.BranchId, out var targetBranchId, out var denied))
        {
            return denied!;
        }

        if (!IsPayrollAuthorizedStaff())
        {
            return Forbid();
        }

        var result = await _payrollRunsAppService.CreateAsync(
            targetBranchId,
            this.GetCurrentUserId(),
            payload,
            HttpContext.RequestAborted);

        return result.Status switch
        {
            PayrollRunEndpointActionStatus.Created => CreatedAtAction(nameof(GetAll), new { branchId = targetBranchId }, result.Data),
            _ => ToActionResult(result)
        };
    }

    [HttpPost("{id:long}/approvals")]
    public async Task<IActionResult> Approve(long id)
    {
        if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
        {
            return denied!;
        }

        if (!IsPayrollAuthorizedStaff())
        {
            return Forbid();
        }

        var result = await _payrollRunsAppService.ApproveAsync(
            id,
            scopedBranchId,
            this.GetCurrentUserId(),
            HttpContext.RequestAborted);
        return ToActionResult(result);
    }

    private bool IsPayrollAuthorizedStaff() =>
        this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "ACCOUNTANT", "FINANCE", "KE_TOAN", "HR_MANAGER", "NHAN_SU");

    private IActionResult ToActionResult<T>(PayrollRunEndpointActionResult<T> result) => result.Status switch
    {
        PayrollRunEndpointActionStatus.NotFound => this.NotFoundProblem(result.ErrorMessage ?? string.Empty),
        PayrollRunEndpointActionStatus.Forbidden => Forbid(),
        PayrollRunEndpointActionStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? string.Empty),
        _ => Ok(result.Data)
    };
}
