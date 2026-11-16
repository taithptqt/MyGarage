using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Bays;
using MyGarage.Application.Authorization;

namespace MyGarage.Controllers;

[ApiController]
[Authorize]
public sealed class VehicleLocationsController(IVehicleLocationService locations) : ControllerBase
{
    [HttpPut("api/technician/vehicle-repairs/{serviceOrderId:long}")]
    [HttpPut("api/v1/technician/vehicle-repairs/{serviceOrderId:long}")]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> ReportRepair(long serviceOrderId, [FromBody] RepairReportRequest request, CancellationToken ct)
    {
        try
        {
            await locations.ReportRepairAsync(serviceOrderId, new(request.PartId, request.Note, request.Completed), this.ToUserActorContext(), ct);
            return Ok(new { message = "Đã cập nhật bộ phận sửa chữa trên sơ đồ 3D." });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("api/customers/me/garage-locations")]
    [HttpGet("api/v1/customers/me/garage-locations")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetMyGarage(CancellationToken ct)
    {
        var customerId = this.GetCurrentUserId();
        if (!customerId.HasValue) return Unauthorized();
        return Ok(await locations.GetCustomerGarageAsync(customerId.Value, ct));
    }

    [HttpGet("api/technician/vehicle-locations")]
    [HttpGet("api/v1/technician/vehicle-locations")]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> GetWorkspace(CancellationToken ct)
    {
        try { return Ok(await locations.GetTechnicianWorkspaceAsync(this.ToUserActorContext(), ct)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("api/technician/vehicle-locations/{serviceOrderId:long}")]
    [HttpPut("api/v1/technician/vehicle-locations/{serviceOrderId:long}")]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> Report(long serviceOrderId, [FromBody] LocationReportRequest request, CancellationToken ct)
    {
        try
        {
            await locations.ReportAsync(serviceOrderId, new VehicleLocationReportInput(request.BayId, request.ExpectedBayId, request.Note), this.ToUserActorContext(), ct);
            return Ok(new { message = "Đã cập nhật vị trí xe cho khách hàng." });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}

public sealed class LocationReportRequest
{
    [Range(1, long.MaxValue)] public long BayId { get; set; }
    public long? ExpectedBayId { get; set; }
    [Required, StringLength(500, MinimumLength = 1)] public string Note { get; set; } = "";
}

public sealed class RepairReportRequest
{
    [Required] public string PartId { get; set; } = "";
    [Required, StringLength(500, MinimumLength = 1)] public string Note { get; set; } = "";
    public bool Completed { get; set; }
}
