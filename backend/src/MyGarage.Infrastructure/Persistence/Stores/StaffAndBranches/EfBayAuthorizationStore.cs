using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Persistence.Stores.StaffAndBranches
{
    public sealed class EfBayAuthorizationStore : IBayAuthorizationStore
    {
        private readonly IGarageContext _context;

        public EfBayAuthorizationStore(IGarageContext context)
        {
            _context = context;
        }

        public Task<long?> GetBayBranchIdAsync(long bayId, CancellationToken cancellationToken = default)
        {
            return _context.Bays
                .AsNoTracking()
                .Where(b => b.Id == bayId)
                .Select(b => (long?)b.BranchId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<long?> GetServiceOrderExecuteBranchIdAsync(long serviceOrderId, CancellationToken cancellationToken = default)
        {
            return _context.ServiceOrders
                .AsNoTracking()
                .Where(so => so.Id == serviceOrderId)
                .Select(so => (long?)so.ExecuteBranchId)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
