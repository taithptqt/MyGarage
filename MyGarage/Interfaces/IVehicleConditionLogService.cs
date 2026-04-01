using MyGarage.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogService : IGenericService<VehicleConditionLog>
    {
        Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId);
        Task<IReadOnlyList<VehicleConditionLog>> GetByOrderAsync(long serviceOrderId);
        Task<IReadOnlyList<VehicleConditionLog>> GetActiveRequestsAsync(string logType);
        Task<VehicleConditionLog?> GetActiveRequestAsync(long serviceOrderId, string logType);
        Task<bool> ServiceOrderExistsAsync(long serviceOrderId);
        Task<bool> TechnicianExistsAsync(long technicianId);
        Task<bool> HasCompletedLogAsync(long serviceOrderId, string logType);
        Task<bool> HasSignedCheckInAsync(long serviceOrderId);
        Task<VehicleConditionLog> CreateRequestAsync(long serviceOrderId, string logType);
        Task MarkRequestAssignedAsync(VehicleConditionLog requestLog, long technicianId);
        Task MarkActiveRequestCompletedAsync(long serviceOrderId, string logType);
    }
}
