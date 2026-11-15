using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Inventory;
using MyGarage.Extensions;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/inventory/parts-page")]
[Route("api/v1/inventory/parts-page")]
public sealed class InventoryPartsPageController : ControllerBase
{
    private readonly IInventoryApplicationService _inventoryAppService;

    public InventoryPartsPageController(IInventoryApplicationService inventoryAppService)
    {
        _inventoryAppService = inventoryAppService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _inventoryAppService.GetPartsPageAsync(search, page, pageSize, cancellationToken);
        var envelope = PaginationExtensions.CreatePagedEnvelope(result.Items, result.Total, result.Page, result.PageSize);
        return Ok(new
        {
            items = envelope.Items,
            total = envelope.Total,
            totalCount = envelope.TotalCount,
            page = envelope.Page,
            pageSize = envelope.PageSize,
            totalPages = envelope.TotalPages,
            hasPreviousPage = envelope.HasPreviousPage,
            hasNextPage = envelope.HasNextPage
        });
    }
}
