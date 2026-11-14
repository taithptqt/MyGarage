using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/branches/admin-list")]
[Route("api/v1/branches/admin-list")]
public sealed class BranchAdministrationController : ControllerBase
{
    private readonly IGarageContext _db;
    private readonly IMapper _mapper;

    public BranchAdministrationController(IGarageContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var all = _db.Branches.AsNoTracking();
        var totalBranches = await all.CountAsync(cancellationToken);
        var activeBranches = await all.CountAsync(x => x.Status != null && x.Status.ToLower() == "active", cancellationToken);
        var filtered = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x => (x.Name ?? "").ToLower().Contains(term) ||
                (x.Address ?? "").ToLower().Contains(term) || (x.Hotline ?? "").ToLower().Contains(term) ||
                (x.Email ?? "").ToLower().Contains(term));
        }
        var total = await filtered.CountAsync(cancellationToken);
        var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var offset = ((long)current - 1) * size;
        var rows = offset >= total ? new List<Branch>() : await filtered.OrderBy(x => x.Id)
            .Skip((int)offset).Take(size).ToListAsync(cancellationToken);
        var mappedItems = _mapper.Map<List<BranchResponseDto>>(rows);
        var envelope = PaginationExtensions.CreatePagedEnvelope(mappedItems, total, current, size);
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
            totalBranches,
            activeBranches
        });
    }
}
