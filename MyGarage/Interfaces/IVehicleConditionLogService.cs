using MyGarage.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogService : IGenericService<VehicleConditionLog>
    {
        Task<IEnumerable<VehicleConditionLog>> GetByServiceOrderIdAsync(long serviceOrderId);
    }
}
