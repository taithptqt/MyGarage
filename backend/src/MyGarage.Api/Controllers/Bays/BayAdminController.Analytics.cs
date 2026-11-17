using Microsoft.AspNetCore.Mvc;

namespace MyGarage.Controllers;

public partial class BayAdminController : ControllerBase
{
    [HttpGet("usage")]
    [HttpGet("/api/v1/admin/bays/usage")]
    public async Task<IActionResult> GetUsage([FromQuery] long? branchId) =>
        ToActionResult(await _facade.GetUsageAsync(branchId, HttpContext.RequestAborted));

    [HttpGet("layout")]
    [HttpGet("/api/v1/admin/bays/layout")]
    public async Task<IActionResult> GetLayout([FromQuery] long? branchId) =>
        ToActionResult(await _facade.GetLayoutAsync(branchId, HttpContext.RequestAborted));

    [HttpGet("alerts")]
    [HttpGet("/api/v1/admin/bays/alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] long? branchId) =>
        ToActionResult(await _facade.GetAlertsAsync(branchId, HttpContext.RequestAborted));

    [HttpGet("suggestions")]
    [HttpGet("/api/v1/admin/bays/suggestions")]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] long? vehicleId,
        [FromQuery] long? branchId,
        [FromQuery] string? preferredArea,
        [FromQuery] long? sourceBayId,
        [FromQuery] string? serviceStatus) =>
        ToActionResult(await _facade.GetSuggestionsAsync(
            vehicleId, branchId, preferredArea, sourceBayId, serviceStatus,
            HttpContext.RequestAborted));
}
