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
[Route("api/vehicle-brands/admin-list")]
[Route("api/v1/vehicle-brands/admin-list")]
public sealed class VehicleBrandAdministrationController : ControllerBase
{
    private readonly IGarageContext _db;
    private readonly IMapper _mapper;

    public VehicleBrandAdministrationController(IGarageContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var all = _db.VehicleBrands.AsNoTracking();
        var totalBrands = await all.CountAsync(cancellationToken);
        var activeBrands = await all.CountAsync(x => x.IsActive == true, cancellationToken);
        var inactiveBrands = await all.CountAsync(x => x.IsActive == false, cancellationToken);
        var filtered = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x => (x.Code ?? "").ToLower().Contains(term) ||
                (x.Name ?? "").ToLower().Contains(term) || (x.CountryOfOrigin ?? "").ToLower().Contains(term));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var offset = ((long)currentPage - 1) * size;
        var rows = offset >= total ? new List<VehicleBrand>() : await filtered.OrderBy(x => x.Id)
            .Skip((int)offset).Take(size).ToListAsync(cancellationToken);
        var mappedItems = _mapper.Map<List<VehicleBrandResponseDto>>(rows);
        var envelope = PaginationExtensions.CreatePagedEnvelope(mappedItems, total, currentPage, size);
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
            totalBrands,
            activeBrands,
            inactiveBrands
        });
    }
}
