using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VehicleRepository : GenericRepository<Vehicle>, IVehicleRepository
    {
        public VehicleRepository(GarageContext context) : base(context)
        {
        }

        public override async Task<IEnumerable<Vehicle>> GetAllAsync()
        {
            return await _context.Vehicles
                .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
                .Where(v => v.IsDeleted != true)
                .ToListAsync();
        }
    }
}
