using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVehicleQueryService : IVehicleQueryService
{
    private readonly IGarageContext _context;

    public EfVehicleQueryService(IGarageContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Vehicle> Items, int Total)> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Vehicles.AsNoTracking()
            .Include(v => v.Model).ThenInclude(m => m.Brand)
            .Where(v => v.IsDeleted != true)
            .OrderBy(v => v.Id);

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? new List<Vehicle>()
            : await query.Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> CustomerOwnsVehicleAsync(long vehicleId, long customerId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(cv => cv.VehicleId == vehicleId && cv.CustomerId == customerId && cv.IsDeleted != true, ct);
    }
}
