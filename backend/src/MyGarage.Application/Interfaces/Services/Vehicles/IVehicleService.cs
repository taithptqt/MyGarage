using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleService : IGenericService<Vehicle>
    {
        List<VehicleResponseDto> MapToDtos(IEnumerable<Vehicle> items);
        Task<VehicleResponseDto?> GetDtoByIdAsync(long id);
        Task<VehicleResponseDto> CreateDtoAsync(VehicleCreateDto itemDto);
        Task<VehicleResponseDto?> UpdateDtoAsync(long id, VehicleUpdateDto itemDto);
    }
}
