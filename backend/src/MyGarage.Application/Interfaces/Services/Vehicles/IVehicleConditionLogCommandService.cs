using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogCommandService
    {
        Task<VehicleConditionLog> CreateRequestAsync(long serviceOrderId, string logType);
        Task MarkRequestAssignedAsync(VehicleConditionLog requestLog, long technicianId);
        Task MarkActiveRequestCompletedAsync(long serviceOrderId, string logType);
        Task MarkServiceOrderCheckedInAsync(long serviceOrderId);
    }
}
