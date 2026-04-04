using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogRepository : IGenericRepository<VehicleConditionLog>
    {
        Task<bool> ExistsAsync(long serviceOrderId, string logType);
        Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId);
    }
}
