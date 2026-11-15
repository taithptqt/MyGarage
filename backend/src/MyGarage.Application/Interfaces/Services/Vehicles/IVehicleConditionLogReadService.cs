using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogReadService
    {
        Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId);
        Task<IReadOnlyList<VehicleConditionLog>> GetByOrderAsync(long serviceOrderId);
        Task<IReadOnlyList<VehicleConditionLogResponseDto>> GetByOrderDtosAsync(long serviceOrderId);
        Task<IReadOnlyList<VehicleConditionLog>> GetActiveRequestsAsync(string logType);
        Task<IReadOnlyList<VehicleConditionLogResponseDto>> GetActiveRequestDtosAsync(string logType);
        Task<VehicleConditionLog?> GetActiveRequestAsync(long serviceOrderId, string logType);
    }
}
