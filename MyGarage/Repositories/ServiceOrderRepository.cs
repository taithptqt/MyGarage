using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Repositories
{
    public class ServiceOrderRepository : GenericRepository<ServiceOrder>, IServiceOrderRepository
    {
        public ServiceOrderRepository(GarageContext context) : base(context)
        {
        }

        public override async Task<IEnumerable<ServiceOrder>> GetAllAsync()
        {
            return await _context.ServiceOrders
                .Include(x => x.Customer)
                .Include(x => x.Vehicle)
                    .ThenInclude(v => v.Model)
                .ToListAsync();
        }

        public override async Task<ServiceOrder?> GetByIdAsync(object id)
        {
            if (id is not long longId)
            {
                return await base.GetByIdAsync(id);
            }

            return await _context.ServiceOrders
                .Include(x => x.Customer)
                .Include(x => x.Vehicle)
                    .ThenInclude(v => v.Model)
                .FirstOrDefaultAsync(x => x.Id == longId);
        }
    }
}
