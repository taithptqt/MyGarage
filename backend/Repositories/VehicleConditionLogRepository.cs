using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Repositories
{
    public class VehicleConditionLogRepository : GenericRepository<VehicleConditionLog>, IVehicleConditionLogRepository
    {
        public VehicleConditionLogRepository(GarageContext context) : base(context)
        {
        }

        public async Task<bool> ExistsAsync(long serviceOrderId, string logType)
        {
            return await _dbSet.AnyAsync(l => l.ServiceOrderId == serviceOrderId && l.LogType == logType);
        }

        public async Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId)
        {
            return await _dbSet
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }
    }
}
