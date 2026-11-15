using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Payroll;

namespace MyGarage.Controllers;

[Route("api/v1/salary-admin")]
[ApiController]
[Authorize(Roles = "Staff")]
public class SalaryAdminController : ControllerBase
{
    private readonly ISalaryAdminCommandFacade _facade;

    public SalaryAdminController(ISalaryAdminCommandFacade facade)
    {
        _facade = facade;
    }

    [HttpGet("profiles")]
    public async Task<IActionResult> GetSalaryProfiles([FromQuery] long? branchId = null, [FromQuery] int? page = null, [FromQuery] int? pageSize = null) =>
        ToActionResult(await _facade.GetSalaryProfilesAsync(branchId, page, pageSize, BuildActorContext(), HttpContext.RequestAborted));

    [HttpPost("profiles")]
    public async Task<IActionResult> CreateSalaryProfile([FromBody] SalaryProfileUpsertDto request) =>
        ToActionResult(await _facade.CreateSalaryProfileAsync(request, BuildActorContext(), HttpContext.RequestAborted));

    [HttpPut("profiles/{id:long}")]
    public async Task<IActionResult> UpdateSalaryProfile(long id, [FromBody] SalaryProfileUpsertDto request) =>
        ToActionResult(await _facade.UpdateSalaryProfileAsync(id, request, BuildActorContext(), HttpContext.RequestAborted));

    [HttpDelete("profiles/{id:long}")]
    public async Task<IActionResult> DeleteSalaryProfile(long id) =>
        ToActionResult(await _facade.DeleteSalaryProfileAsync(id, BuildActorContext(), HttpContext.RequestAborted));

    [HttpGet("requests")]
    public async Task<IActionResult> GetInitialSalaryRequests(
        [FromQuery] string? status = null,
        [FromQuery] long? branchId = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null) =>
        ToActionResult(await _facade.GetInitialSalaryRequestsAsync(status, branchId, page, pageSize, BuildActorContext(), HttpContext.RequestAborted));

    [HttpPost("requests/{id:long}/approvals")]
    public async Task<IActionResult> ApproveInitialSalaryRequest(long id, [FromBody] SalaryApprovalDecisionDto request) =>
        ToActionResult(await _facade.ApproveInitialSalaryRequestAsync(id, request, BuildActorContext(), HttpContext.RequestAborted));

    [HttpPost("requests/{id:long}/rejections")]
    public async Task<IActionResult> RejectInitialSalaryRequest(long id, [FromBody] SalaryApprovalDecisionDto request) =>
        ToActionResult(await _facade.RejectInitialSalaryRequestAsync(id, request, BuildActorContext(), HttpContext.RequestAborted));

    private SalaryAdminActorContext BuildActorContext()
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        long? userId = long.TryParse(rawUserId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

        return new SalaryAdminActorContext(
            userId,
            User.FindFirstValue(ClaimTypes.Role),
            User.FindFirstValue("role_code"),
            User.FindFirstValue("role_name"));
    }

    private IActionResult ToActionResult(SalaryAdminCommandResult result) => result.StatusCode switch
    {
        200 => Ok(result.Payload),
        400 => this.BadRequestProblem(result.ProblemMessage ?? "Bad request."),
        403 => Forbid(),
        404 => !string.IsNullOrWhiteSpace(result.ProblemMessage) ? this.NotFoundProblem(result.ProblemMessage) : NotFound(),
        409 => this.ConflictProblem(result.ProblemMessage ?? "Conflict."),
        _ => StatusCode(result.StatusCode, result.Payload)
    };
}
