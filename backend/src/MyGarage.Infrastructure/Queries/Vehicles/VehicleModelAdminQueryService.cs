using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVehicleModelAdminQueryService : IVehicleModelAdminQueryService
{
    private readonly IGarageContext _context;

    public EfVehicleModelAdminQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<VehicleModelAdminPageData> GetAdminPageAsync(string? search, int currentPage, int pageSize, CancellationToken ct = default)
    {
        var all = _context.VehicleModels.AsNoTracking();
        var totalModels = await all.CountAsync(ct);
        var brandCount = await all.Select(x => x.BrandId).Distinct().CountAsync(ct);
        var activeModels = await all.CountAsync(x => x.IsDeleted != true, ct);

        var filtered = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x =>
                (x.ModelName ?? "").ToLower().Contains(term) ||
                (x.Segment ?? "").ToLower().Contains(term) ||
                (x.FuelType ?? "").ToLower().Contains(term) ||
                (x.Brand.Name != null && x.Brand.Name != ""
                    ? x.Brand.Name
                    : x.Brand.Code != null && x.Brand.Code != ""
                        ? x.Brand.Code
                        : "Hãng #" + x.BrandId.ToString())
                    .ToLower().Contains(term));
        }

        var total = await filtered.CountAsync(ct);
        var offset = ((long)currentPage - 1) * pageSize;
        var rows = offset >= total
            ? new List<VehicleModel>()
            : await filtered.OrderBy(x => x.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(ct);

        return new VehicleModelAdminPageData(rows, total, totalModels, brandCount, activeModels);
    }
}
