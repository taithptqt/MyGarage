using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Retail;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.Retail;

public sealed class EfRetailOrderAuthorizationStore : IRetailOrderAuthorizationStore
{
    private readonly IGarageContext _context;

    public EfRetailOrderAuthorizationStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<RetailOrderAuthInfo?> GetRetailOrderAuthInfoAsync(long retailOrderId, CancellationToken ct = default)
    {
        return _context.RetailOrders
            .AsNoTracking()
            .Where(o => o.Id == retailOrderId)
            .Select(o => new RetailOrderAuthInfo(o.BranchId, o.CustomerId, o.Status ?? string.Empty))
            .FirstOrDefaultAsync(ct);
    }

    public Task<long?> GetRetailReturnBranchIdAsync(long retailReturnId, CancellationToken ct = default)
    {
        return _context.RetailReturns
            .AsNoTracking()
            .Where(r => r.Id == retailReturnId)
            .Select(r => (long?)r.RetailOrder.BranchId)
            .FirstOrDefaultAsync(ct);
    }
}
