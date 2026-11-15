using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfSupplierQueryService : ISupplierQueryService
{
    private readonly IGarageContext _context;

    public EfSupplierQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Supplier> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Suppliers
            .AsNoTracking()
            .Where(x => x.IsDeleted != true)
            .OrderByDescending(x => x.Id);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
