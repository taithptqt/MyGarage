using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogValidationService
    {
        Task<bool> ServiceOrderExistsAsync(long serviceOrderId);
        Task<bool> TechnicianExistsAsync(long technicianId);
        Task<bool> HasCompletedLogAsync(long serviceOrderId, string logType);
        Task<bool> HasSignedCheckInAsync(long serviceOrderId);
    }
}
