using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVehicleBrandAdminQueryService : IVehicleBrandAdminQueryService
{
    private readonly IGarageContext _context;

    public EfVehicleBrandAdminQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<VehicleBrandAdminPageData> GetAdminPageAsync(string? search, int currentPage, int pageSize, CancellationToken ct = default)
    {
        var all = _context.VehicleBrands.AsNoTracking();
        var totalBrands = await all.CountAsync(ct);
        var activeBrands = await all.CountAsync(x => x.IsActive == true, ct);
        var inactiveBrands = await all.CountAsync(x => x.IsActive == false, ct);

        var filtered = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            filtered = filtered.Where(x =>
                (x.Code ?? "").ToLower().Contains(term) ||
                (x.Name ?? "").ToLower().Contains(term) ||
                (x.CountryOfOrigin ?? "").ToLower().Contains(term));
        }

        var total = await filtered.CountAsync(ct);
        var offset = ((long)currentPage - 1) * pageSize;
        var rows = offset >= total
            ? new List<VehicleBrand>()
            : await filtered.OrderBy(x => x.Id)
                .Skip((int)offset)
                .Take(pageSize)
                .ToListAsync(ct);

        return new VehicleBrandAdminPageData(rows, total, totalBrands, activeBrands, inactiveBrands);
    }
}
