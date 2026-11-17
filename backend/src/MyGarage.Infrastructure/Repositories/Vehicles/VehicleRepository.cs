using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VehicleRepository : GenericRepository<Vehicle>, IVehicleRepository
    {
        public VehicleRepository(IGarageContext context) : base(context)
        {
        }

        private IQueryable<Vehicle> VisibleVehicles() => _context.Vehicles
            .Include(v => v.Model)
            .ThenInclude(m => m.Brand)
            .Where(v => v.IsDeleted != true);

        public override async Task<IEnumerable<Vehicle>> GetAllAsync()
            => await VisibleVehicles().ToListAsync();

        public override Task<(IReadOnlyList<Vehicle> Items, int Total)> GetPageByIdAsync(int page, int pageSize)
            => GetOrderedPageAsync(VisibleVehicles().AsNoTracking().OrderBy(v => v.Id), page, pageSize);
    }
}

