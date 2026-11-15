using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Bays;

namespace MyGarage.Controllers;

public partial class BayAdminController : ControllerBase
{
    [HttpPost("bays/{id:long}/vehicle-assignments")]
    public async Task<IActionResult> AssignVehicle(long id, [FromBody] AssignVehicleRequest request) =>
        ToActionResult(await _facade.AssignVehicleAsync(
            id, request, this.GetCurrentUserId(), _environment.ContentRootPath, HttpContext.RequestAborted));

    [HttpPost("bays/{id:long}/vehicle-removals")]
    public async Task<IActionResult> RemoveVehicleFromBay(long id, [FromBody] RemoveVehicleRequest request) =>
        ToActionResult(await _facade.RemoveVehicleFromBayAsync(
            id, request, this.GetCurrentUserId(), _environment.ContentRootPath, HttpContext.RequestAborted));

    [HttpPost("bay-transfers")]
    public async Task<IActionResult> MoveVehicle([FromBody] MoveVehicleRequest request) =>
        ToActionResult(await _facade.MoveVehicleAsync(
            request, this.GetCurrentUserId(), _environment.ContentRootPath, HttpContext.RequestAborted));
}
