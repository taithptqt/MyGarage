using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.StaffAndBranches
{
    public sealed class EfStaffStore : IStaffStore
    {
        private readonly IGarageContext _context;

        public EfStaffStore(IGarageContext context)
        {
            _context = context;
        }

        public Task<Staff?> GetStaffSnapshotByIdAsync(long staffId, CancellationToken cancellationToken = default)
        {
            return _context.Staffs
                .AsNoTracking()
                .SingleOrDefaultAsync(staff => staff.Id == staffId, cancellationToken);
        }
    }
}
