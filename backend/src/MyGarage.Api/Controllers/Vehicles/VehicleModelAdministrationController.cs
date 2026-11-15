using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/vehicle-models/admin-list")]
public sealed class VehicleModelAdministrationController : ControllerBase
{
    private readonly IVehicleModelAdminQueryService _vehicleModelAdminQueryService;
    private readonly IVehicleModelService _vehicleModelService;

    public VehicleModelAdministrationController(
        IVehicleModelAdminQueryService vehicleModelAdminQueryService,
        IVehicleModelService vehicleModelService)
    {
        _vehicleModelAdminQueryService = vehicleModelAdminQueryService;
        _vehicleModelService = vehicleModelService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var result = await _vehicleModelAdminQueryService.GetAdminPageAsync(search, currentPage, size, cancellationToken);
        var mappedItems = _vehicleModelService.MapToDtos(result.Rows);
        var envelope = PaginationExtensions.CreatePagedEnvelope(mappedItems, result.Total, currentPage, size);
        return Ok(new
        {
            items = envelope.Items,
            total = envelope.Total,
            totalCount = envelope.TotalCount,
            page = envelope.Page,
            pageSize = envelope.PageSize,
            totalPages = envelope.TotalPages,
            hasPreviousPage = envelope.HasPreviousPage,
            hasNextPage = envelope.HasNextPage,
            totalModels = result.TotalModels,
            brandCount = result.BrandCount,
            activeModels = result.ActiveModels
        });
    }
}
