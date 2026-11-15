using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleBrandService : IGenericService<VehicleBrand>
    {
        Task<IEnumerable<VehicleBrandResponseDto>> GetAllDtosAsync();
        List<VehicleBrandResponseDto> MapToDtos(IEnumerable<VehicleBrand> items);
        Task<VehicleBrandResponseDto?> GetDtoByIdAsync(long id);
        Task<VehicleBrandResponseDto> CreateDtoAsync(VehicleBrandCreateDto itemDto);
        Task<VehicleBrandResponseDto?> UpdateDtoAsync(long id, VehicleBrandUpdateDto itemDto);
    }
}
