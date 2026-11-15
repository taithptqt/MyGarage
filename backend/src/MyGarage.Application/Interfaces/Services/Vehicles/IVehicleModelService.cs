using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleModelService : IGenericService<VehicleModel>
    {
        Task<IEnumerable<VehicleModelResponseDto>> GetAllDtosAsync();
        List<VehicleModelResponseDto> MapToDtos(IEnumerable<VehicleModel> items);
        Task<VehicleModelResponseDto?> GetDtoByIdAsync(long id);
        Task<VehicleModelResponseDto> CreateDtoAsync(VehicleModelCreateDto itemDto);
        Task<VehicleModelResponseDto?> UpdateDtoAsync(long id, VehicleModelUpdateDto itemDto);
    }
}
