using System.Threading.Tasks;
using MyGarage.DTOs.Responses.Execution;

namespace MyGarage.Interfaces
{
    public interface IVehicleHistoryService
    {
        Task<VehicleServiceHistoryResponseDto?> GetServiceHistoryAsync(long vehicleId, int page = 1, int pageSize = 20);
    }
}
