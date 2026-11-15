using Microsoft.AspNetCore.Mvc;

namespace MyGarage.Controllers;

public partial class BayAdminController : ControllerBase
{
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage([FromQuery] long? branchId) =>
        ToActionResult(await _facade.GetUsageAsync(branchId, _environment.ContentRootPath, HttpContext.RequestAborted));

    [HttpGet("layout")]
    public async Task<IActionResult> GetLayout([FromQuery] long? branchId) =>
        ToActionResult(await _facade.GetLayoutAsync(branchId, _environment.ContentRootPath, HttpContext.RequestAborted));

    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] long? branchId) =>
        ToActionResult(await _facade.GetAlertsAsync(branchId, _environment.ContentRootPath, HttpContext.RequestAborted));

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] long? vehicleId,
        [FromQuery] long? branchId,
        [FromQuery] string? preferredArea,
        [FromQuery] long? sourceBayId,
        [FromQuery] string? serviceStatus) =>
        ToActionResult(await _facade.GetSuggestionsAsync(
            vehicleId, branchId, preferredArea, sourceBayId, serviceStatus,
            _environment.ContentRootPath, HttpContext.RequestAborted));
}
