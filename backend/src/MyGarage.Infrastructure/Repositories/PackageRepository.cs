using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class PackageRepository : GenericRepository<Package>, IPackageRepository
    {
        public PackageRepository(IGarageContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Package>> GetAllWithItemsAsync()
        {
            return await _context.Packages
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                    .ThenInclude(item => item.StandardService)
                .Where(x => x.IsDeleted != true && x.IsActive != false)
                .OrderBy(x => x.CategoryId)
                .ThenBy(x => x.Code)
                .ToListAsync();
        }

        public async Task<Package?> GetByIdWithItemsAsync(long id)
        {
            return await _context.Packages
                .AsNoTracking()
                .Include(x => x.Category)
                .Include(x => x.PackageItems.Where(item => item.IsDeleted != true && item.IsActive != false))
                    .ThenInclude(item => item.StandardService)
                .FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true);
        }
    }
}
