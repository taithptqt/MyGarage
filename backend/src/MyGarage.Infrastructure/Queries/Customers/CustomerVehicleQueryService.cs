using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfCustomerVehicleQueryService : ICustomerVehicleQueryService
{
    private readonly IGarageContext _context;

    public EfCustomerVehicleQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<CustomerVehicle> Items, int Total, int Page, int PageSize)> GetPagedAsync(
        long? customerId,
        int? page,
        int? pageSize,
        CancellationToken ct = default)
    {
        var query = _context.CustomerVehicles.AsNoTracking().AsQueryable();
        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
        var total = await query.CountAsync(ct);
        var offset = ((long)normalizedPage - 1) * normalizedSize;
        var items = offset >= total
            ? new List<CustomerVehicle>()
            : await query.OrderBy(x => x.Id)
                .Skip((int)offset)
                .Take(normalizedSize)
                .ToListAsync(ct);

        return (items, total, normalizedPage, normalizedSize);
    }
}
