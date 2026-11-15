using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/vehicle-brands/admin-list")]
public sealed class VehicleBrandAdministrationController : ControllerBase
{
    private readonly IVehicleBrandAdminQueryService _vehicleBrandAdminQueryService;
    private readonly IVehicleBrandService _vehicleBrandService;

    public VehicleBrandAdministrationController(
        IVehicleBrandAdminQueryService vehicleBrandAdminQueryService,
        IVehicleBrandService vehicleBrandService)
    {
        _vehicleBrandAdminQueryService = vehicleBrandAdminQueryService;
        _vehicleBrandService = vehicleBrandService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var result = await _vehicleBrandAdminQueryService.GetAdminPageAsync(search, currentPage, size, cancellationToken);
        var mappedItems = _vehicleBrandService.MapToDtos(result.Rows);
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
            totalBrands = result.TotalBrands,
            activeBrands = result.ActiveBrands,
            inactiveBrands = result.InactiveBrands
        });
    }
}
