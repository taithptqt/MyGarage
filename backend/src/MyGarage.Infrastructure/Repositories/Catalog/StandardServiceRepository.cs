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

        private IQueryable<StandardService> VisibleServices() =>
            _context.StandardServices.AsNoTracking().Where(service =>
                service.IsDeleted != true && service.IsActive != false && !service.Code.StartsWith("PKG-"));

        public override async Task<IEnumerable<StandardService>> GetAllAsync()
            => await VisibleServices().ToListAsync();

        public override Task<(IReadOnlyList<StandardService> Items, int Total)> GetPageByIdAsync(int page, int pageSize)
            => GetOrderedPageAsync(VisibleServices().OrderBy(service => service.Id), page, pageSize);
    }
}
