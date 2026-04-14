using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Repositories
{
    public class StandardServiceRepository : GenericRepository<StandardService>, IStandardServiceRepository
    {
        public StandardServiceRepository(IGarageContext context) : base(context)
        {
        }

        public override async Task<IEnumerable<StandardService>> GetAllAsync()
        {
            return await _context.StandardServices
                .AsNoTracking()
                .Where(service =>
                    service.IsDeleted != true &&
                    service.IsActive != false &&
                    !service.Code.StartsWith("PKG-"))
                .ToListAsync();
        }
    }
}
