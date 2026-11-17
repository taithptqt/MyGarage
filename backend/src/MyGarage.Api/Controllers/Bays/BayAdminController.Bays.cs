using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Bays;

namespace MyGarage.Controllers;

public partial class BayAdminController : ControllerBase
{
    [HttpPost("bays")]
    [HttpPost("/api/v1/admin/bays")]
    public async Task<IActionResult> CreateBay([FromBody] BayUpsertRequest request) =>
        ToActionResult(await _facade.CreateBayAsync(
            request, this.GetCurrentUserId(), HttpContext.RequestAborted));

    [HttpPut("bays/{id:long}")]
    [HttpPut("/api/v1/admin/bays/{id:long}")]
    public async Task<IActionResult> UpdateBay(long id, [FromBody] BayUpsertRequest request) =>
        ToActionResult(await _facade.UpdateBayAsync(
            id, request, this.GetCurrentUserId(), HttpContext.RequestAborted));

    [HttpDelete("bays/{id:long}")]
    [HttpDelete("/api/v1/admin/bays/{id:long}")]
    public async Task<IActionResult> HideBay(long id) =>
        ToActionResult(await _facade.HideBayAsync(id, this.GetCurrentUserId(), HttpContext.RequestAborted));

    [HttpPut("bays/{id:long}/locks")]
    [HttpPut("/api/v1/admin/bays/{id:long}/locks")]
    public async Task<IActionResult> LockBay(long id) =>
        ToActionResult(await _facade.LockBayAsync(id, this.GetCurrentUserId(), HttpContext.RequestAborted));

    [HttpDelete("bays/{id:long}/locks")]
    [HttpDelete("/api/v1/admin/bays/{id:long}/locks")]
    public async Task<IActionResult> UnlockBay(long id) =>
        ToActionResult(await _facade.UnlockBayAsync(id, this.GetCurrentUserId(), HttpContext.RequestAborted));

    [HttpPatch("bays/{id:long}/status")]
    [HttpPatch("/api/v1/admin/bays/{id:long}/status")]
    public async Task<IActionResult> UpdateBayStatus(long id, [FromBody] UpdateBayStatusRequest request) =>
        ToActionResult(await _facade.UpdateBayStatusAsync(
            id, request, this.GetCurrentUserId(), HttpContext.RequestAborted));
}
