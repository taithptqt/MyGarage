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
[Route("api/vehicle-models/admin-list")]
[Route("api/v1/vehicle-models/admin-list")]
public sealed class VehicleModelAdministrationController : ControllerBase
{
    private readonly IGarageContext _db;
    private readonly IMapper _mapper;

    public VehicleModelAdministrationController(IGarageContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? search, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var all = _db.VehicleModels.AsNoTracking();
        var totalModels = await all.CountAsync(cancellationToken);
        var brandCount = await all.Select(x => x.BrandId).Distinct().CountAsync(cancellationToken);
        var activeModels = await all.CountAsync(x => x.IsDeleted != true, cancellationToken);
        var filtered = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x => (x.ModelName ?? "").ToLower().Contains(term) ||
                (x.Segment ?? "").ToLower().Contains(term) || (x.FuelType ?? "").ToLower().Contains(term) ||
                (x.Brand.Name != null && x.Brand.Name != "" ? x.Brand.Name :
                    x.Brand.Code != null && x.Brand.Code != "" ? x.Brand.Code : "Hãng #" + x.BrandId.ToString())
                    .ToLower().Contains(term));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var (currentPage, size) = PaginationExtensions.NormalizePagination(page, pageSize, 10, 100);
        var offset = ((long)currentPage - 1) * size;
        var rows = offset >= total ? new List<VehicleModel>() : await filtered.OrderBy(x => x.Id)
            .Skip((int)offset).Take(size).ToListAsync(cancellationToken);
        var mappedItems = _mapper.Map<List<VehicleModelResponseDto>>(rows);
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
            totalModels,
            brandCount,
            activeModels
        });
    }
}
