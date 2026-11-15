using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[Route("api/v1/vehicle-admin")]
[Route("api/v1/admin/vehicles")]
[ApiController]
[Authorize(Policy = "AdminStaffOnly")]
public class VehicleAdminController : ControllerBase
{
    private readonly IVehicleAdminCommandFacade _facade;
    private readonly IWebHostEnvironment _environment;

    public VehicleAdminController(IVehicleAdminCommandFacade facade, IWebHostEnvironment environment)
    {
        _facade = facade;
        _environment = environment;
    }

    [HttpGet("lookups")]
    public async Task<IActionResult> GetLookups(CancellationToken ct)
    {
        return Ok(await _facade.GetLookupsAsync(_environment.ContentRootPath, ct));
    }

    [HttpGet("list")]
    [HttpGet("vehicles")]
    public async Task<IActionResult> GetList(
        [FromQuery] long? branchId,
        [FromQuery] long? brandId,
        [FromQuery] long? modelId,
        [FromQuery] string? color,
        [FromQuery] int? manufactureYear,
        [FromQuery] string? status,
        [FromQuery] long? bayId,
        [FromQuery] long? staffId,
        [FromQuery] DateTime? checkInFrom,
        [FromQuery] DateTime? checkInTo,
        [FromQuery] string? search,
        [FromQuery] string? sortBy = "checkin",
        [FromQuery] string? sortDirection = "desc",
        [FromQuery] bool includeHidden = false,
        CancellationToken ct = default)
    {
        var query = new VehicleAdminListQuery(
            branchId,
            brandId,
            modelId,
            color,
            manufactureYear,
            status,
            bayId,
            staffId,
            checkInFrom,
            checkInTo,
            search,
            sortBy,
            sortDirection,
            includeHidden);

        return Ok(await _facade.GetListAsync(query, _environment.ContentRootPath, ct));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] long? branchId, CancellationToken ct)
    {
        return Ok(await _facade.GetSummaryAsync(branchId, _environment.ContentRootPath, ct));
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] long? branchId, CancellationToken ct)
    {
        return Ok(await _facade.GetAlertsAsync(branchId, _environment.ContentRootPath, ct));
    }

    [HttpGet("vehicles/{id:long}")]
    public async Task<IActionResult> GetVehicleDetail(long id, [FromQuery] int takeHistory = 200, CancellationToken ct = default)
    {
        return ToActionResult(await _facade.GetVehicleDetailAsync(id, takeHistory, _environment.ContentRootPath, ct));
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        [FromQuery] long? vehicleId,
        [FromQuery] long? branchId,
        [FromQuery] int take = 300,
        CancellationToken ct = default)
    {
        return Ok(await _facade.GetHistoryAsync(vehicleId, branchId, take, _environment.ContentRootPath, ct));
    }

    [HttpPost("vehicles")]
    public async Task<IActionResult> CreateVehicle([FromBody] VehicleUpsertRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.CreateVehicleAsync(request, BuildActorContext(), ct));
    }

    [HttpPut("vehicles/{id:long}")]
    public async Task<IActionResult> UpdateVehicle(long id, [FromBody] VehicleUpsertRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.UpdateVehicleAsync(id, request, BuildActorContext(), ct));
    }

    [HttpPut("vehicles/{id:long}/edit-locks")]
    public async Task<IActionResult> LockEdit(long id, CancellationToken ct)
    {
        return ToActionResult(await _facade.LockEditAsync(id, BuildActorContext(), ct));
    }

    [HttpDelete("vehicles/{id:long}/edit-locks")]
    public async Task<IActionResult> UnlockEdit(long id, CancellationToken ct)
    {
        return ToActionResult(await _facade.UnlockEditAsync(id, BuildActorContext(), ct));
    }

    [HttpDelete("vehicles/{id:long}")]
    public async Task<IActionResult> HideVehicle(long id, CancellationToken ct)
    {
        return ToActionResult(await _facade.HideVehicleAsync(id, BuildActorContext(), ct));
    }

    [HttpPatch("vehicles/{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] ChangeStatusRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.UpdateStatusAsync(id, request, BuildActorContext(), ct));
    }

    [HttpPatch("vehicles/{id:long}/owner")]
    public async Task<IActionResult> ChangeOwner(long id, [FromBody] ChangeOwnerRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.ChangeOwnerAsync(id, request, BuildActorContext(), ct));
    }

    [HttpPost("vehicles/{id:long}/staff-assignments")]
    public async Task<IActionResult> AssignStaff(long id, [FromBody] AssignStaffRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.AssignStaffAsync(id, request, BuildActorContext(), ct));
    }

    [HttpPost("vehicles/{id:long}/bay-assignments")]
    public async Task<IActionResult> AssignBay(long id, [FromBody] AssignBayRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.AssignBayAsync(id, request, BuildActorContext(), ct));
    }

    [HttpPost("vehicles/{id:long}/bay-transfers")]
    public async Task<IActionResult> MoveBay(long id, [FromBody] MoveBayRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.MoveBayAsync(id, request, BuildActorContext(), ct));
    }

    [HttpPost("vehicles/{id:long}/bay-removals")]
    public async Task<IActionResult> RemoveBay(long id, [FromBody] RemoveBayRequest request, CancellationToken ct)
    {
        return ToActionResult(await _facade.RemoveBayAsync(id, request, BuildActorContext(), ct));
    }

    private VehicleAdminActorContext BuildActorContext()
    {
        var displayName = User.FindFirstValue(ClaimTypes.Name)
                          ?? User.FindFirstValue("fullName")
                          ?? User.FindFirstValue("username");
        return new VehicleAdminActorContext(_environment.ContentRootPath, this.GetCurrentUserId(), displayName);
    }

    private IActionResult ToActionResult(VehicleAdminCommandResult result)
    {
        return result.StatusCode switch
        {
            200 => Ok(result.Payload),
            400 => BadRequest(result.Payload),
            404 => NotFound(result.Payload),
            _ => StatusCode(result.StatusCode, result.Payload)
        };
    }
}
