using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Bays;

namespace MyGarage.Controllers;

[Route("api/v1/bay-admin")]
[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
public partial class BayAdminController : ControllerBase
{
    private readonly IBayAdminCommandFacade _facade;

    public BayAdminController(IBayAdminCommandFacade facade)
    {
        _facade = facade;
    }

    private IActionResult ToActionResult(BayAdminCommandResult result) => result.StatusCode switch
    {
        200 => Ok(result.Payload),
        400 => BadRequest(result.Payload),
        404 => NotFound(result.Payload),
        _ => StatusCode(result.StatusCode, result.Payload)
    };

    [HttpGet("lookups")]
    [HttpGet("/api/v1/admin/bays/lookups")]
    public async Task<IActionResult> GetLookups([FromQuery] int takeVehicles = 500) =>
        ToActionResult(await _facade.GetLookupsAsync(takeVehicles, HttpContext.RequestAborted));

    [HttpGet("list")]
    [HttpGet("bays")]
    [HttpGet("/api/v1/admin/bays")]
    public async Task<IActionResult> GetList(
        [FromQuery] long? branchId,
        [FromQuery] string? search,
        [FromQuery] string? area,
        [FromQuery] string? status,
        [FromQuery] string? sortBy = "sequence",
        [FromQuery] string? sortDirection = "asc",
        [FromQuery] bool includeHidden = false) =>
        ToActionResult(await _facade.GetListAsync(
            branchId, search, area, status, sortBy, sortDirection, includeHidden,
            HttpContext.RequestAborted));

    [HttpGet("bays/{id:long}")]
    [HttpGet("/api/v1/admin/bays/{id:long}")]
    public async Task<IActionResult> GetById(long id) =>
        ToActionResult(await _facade.GetByIdAsync(id, HttpContext.RequestAborted));

    [HttpGet("summary")]
    [HttpGet("/api/v1/admin/bays/summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long? branchId) =>
        ToActionResult(await _facade.GetSummaryAsync(branchId, HttpContext.RequestAborted));

    [HttpGet("bays/{id:long}/vehicles")]
    [HttpGet("/api/v1/admin/bays/{id:long}/vehicles")]
    public async Task<IActionResult> GetBayVehicles(long id) =>
        ToActionResult(await _facade.GetBayVehiclesAsync(id, HttpContext.RequestAborted));

    [HttpGet("bays/{id:long}/history")]
    [HttpGet("/api/v1/admin/bays/{id:long}/history")]
    public async Task<IActionResult> GetBayHistory(long id, [FromQuery] int take = 200) =>
        ToActionResult(await _facade.GetBayHistoryAsync(id, take, HttpContext.RequestAborted));
}
