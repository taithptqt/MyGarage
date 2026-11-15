using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleConditionLogCrudService : IGenericService<VehicleConditionLog>
    {
        Task<IEnumerable<VehicleConditionLogResponseDto>> GetAllDtosAsync();
        Task<VehicleConditionLogResponseDto?> GetDtoByIdAsync(long id);
    }
}
