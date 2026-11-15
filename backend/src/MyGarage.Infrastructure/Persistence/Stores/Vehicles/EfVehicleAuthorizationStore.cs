using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Vehicles;

public sealed class EfVehicleAuthorizationStore : IVehicleAuthorizationStore
{
    private readonly IGarageContext _context;

    public EfVehicleAuthorizationStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<bool> CustomerHasVehicleAccessAsync(long vehicleId, long customerId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(cv => cv.VehicleId == vehicleId && cv.CustomerId == customerId && cv.IsDeleted != true, ct);
    }

    public async Task<bool> CustomerIsCurrentOwnerAsync(long vehicleId, long customerId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(cv => cv.VehicleId == vehicleId && cv.CustomerId == customerId && cv.IsCurrentOwner == true && cv.IsDeleted != true, ct);
    }

    public async Task<bool> CustomerOwnsCustomerVehicleAsync(long customerVehicleId, long customerId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(cv => cv.Id == customerVehicleId && cv.CustomerId == customerId && cv.IsDeleted != true, ct);
    }
}
