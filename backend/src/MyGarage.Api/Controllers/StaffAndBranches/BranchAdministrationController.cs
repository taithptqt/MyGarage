using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/v1/branches/admin-list")]
public sealed class BranchAdministrationController : ControllerBase
{
    private readonly IBranchAdminQueryService _branchAdminQueryService;
    private readonly IBranchService _branchService;

    public BranchAdministrationController(IBranchAdminQueryService branchAdminQueryService, IBranchService branchService)
    {
        _branchAdminQueryService = branchAdminQueryService;
        _branchService = branchService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var result = await _branchAdminQueryService.GetAdminPageAsync(search, current, size, cancellationToken);
        var mappedItems = _branchService.MapToDtos(result.Rows);
        var envelope = PaginationExtensions.CreatePagedEnvelope(mappedItems, result.Total, current, size);
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
            totalBranches = result.TotalBranches,
            activeBranches = result.ActiveBranches
        });
    }
}
