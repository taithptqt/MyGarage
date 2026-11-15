using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Inventory;

namespace MyGarage.Controllers;

[ApiController]
[Route("api/inventory-management/catalog")]
[Route("api/v1/inventory-management/catalog")]
[Authorize(Roles = "Staff")]
public sealed class InventoryCatalogController : ControllerBase
{
    private readonly IInventoryApplicationService _inventoryAppService;

    public InventoryCatalogController(IInventoryApplicationService inventoryAppService)
    {
        _inventoryAppService = inventoryAppService;
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> GetPage(string kind, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var result = await _inventoryAppService.GetCatalogPageAsync(kind, page, pageSize, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            items = result.Items,
            total = result.Total,
            summary = new
            {
                productCount = result.Summary.ProductCount,
                classified = result.Summary.Classified,
                unclassified = result.Summary.Unclassified,
                categoryCount = result.Summary.CategoryCount,
                supplierCount = result.Summary.SupplierCount
            }
        });
    }

    [HttpGet("products/export")]
    public async Task ExportProducts(CancellationToken cancellationToken)
    {
        Response.ContentType = "text/csv; charset=utf-8";
        await _inventoryAppService.ExportProductsCsvAsync(Response.Body, cancellationToken);
    }
}
