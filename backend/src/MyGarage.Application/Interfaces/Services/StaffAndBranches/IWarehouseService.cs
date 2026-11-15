using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IWarehouseService : IGenericService<Warehouse>
    {
        Task<IEnumerable<WarehouseResponseDto>> GetAllDtosAsync();
        Task<WarehouseResponseDto?> GetDtoByIdAsync(long id);
        Task<WarehouseResponseDto> CreateDtoAsync(WarehouseCreateDto itemDto);
        Task<WarehouseResponseDto?> UpdateDtoAsync(long id, WarehouseUpdateDto itemDto);
    }
}
