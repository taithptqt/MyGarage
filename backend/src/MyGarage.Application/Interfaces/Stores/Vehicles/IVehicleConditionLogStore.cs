using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public interface IVehicleConditionLogStore
{
    Task<bool> ServiceOrderExistsAsync(long serviceOrderId);
    Task<bool> TechnicianExistsAsync(long technicianId);
    Task<IReadOnlyList<VehicleConditionLog>> GetByOrderAsync(long serviceOrderId);
    Task<IReadOnlyList<VehicleConditionLog>> GetActiveRequestsAsync(string normalizedLogType);
    Task<VehicleConditionLog?> GetActiveRequestAsync(long serviceOrderId, string normalizedLogType);
    Task<bool> HasCompletedLogAsync(long serviceOrderId, string normalizedLogType);
    Task<bool> HasSignedCheckInAsync(long serviceOrderId);
    Task AddAndSaveAsync(VehicleConditionLog requestLog);
    Task UpdateAndSaveAsync(VehicleConditionLog requestLog);
    Task<ServiceOrder?> FindServiceOrderAsync(long serviceOrderId);
    Task UpdateServiceOrderAndSaveAsync(ServiceOrder serviceOrder);
}
