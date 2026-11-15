using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInventoryService : IGenericService<Inventory>
    {
        List<InventoryResponseDto> MapToDtos(IEnumerable<Inventory> items);
        Task<InventoryResponseDto?> GetDtoByIdAsync(long id);
        Task<InventoryResponseDto> CreateDtoAsync(InventoryCreateDto itemDto);
        Task<InventoryResponseDto?> UpdateDtoAsync(long id, InventoryUpdateDto itemDto);
    }
}
