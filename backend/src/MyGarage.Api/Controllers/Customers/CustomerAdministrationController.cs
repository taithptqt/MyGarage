using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/customers/admin-list")]
public sealed class CustomerAdministrationController : ControllerBase
{
    private readonly ICustomerQueryService _queryService;
    private readonly ICustomerService _customerService;

    public CustomerAdministrationController(ICustomerQueryService queryService, ICustomerService customerService)
    {
        _queryService = queryService;
        _customerService = customerService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] long? branchId, [FromQuery] string? search,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied)) return denied!;
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var result = await _queryService.GetAdminPageAsync(scopedBranchId, search, currentPage, size, cancellationToken);
        var items = result.Rows.Select(x => new
        {
            customer = _customerService.MapToDto(x.Customer),
            tierName = x.TierName ?? "-",
            customerVehicleId = x.Primary?.LinkId,
            vehicleId = x.Primary?.VehicleId,
            brandId = x.Primary?.Exists == true ? x.Primary.BrandId : (long?)null,
            brandName = x.Primary?.Exists == true ? x.Primary.BrandName : null,
            modelId = x.Primary?.Exists == true ? x.Primary.ModelId : (long?)null,
            modelName = x.Primary?.Exists == true ? x.Primary.ModelName : null,
            licensePlate = x.Primary?.Exists == true ? x.Primary.Plate : null,
            color = x.Primary?.Exists == true ? x.Primary.Color : null,
            manufactureYear = x.Primary?.Exists == true ? x.Primary.Year : null
        });
        var envelope = PaginationExtensions.CreatePagedEnvelope(items, result.Total, currentPage, size);
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
            totalCustomers = result.TotalCustomers,
            activeCustomers = result.ActiveCustomers
        });
    }
}
