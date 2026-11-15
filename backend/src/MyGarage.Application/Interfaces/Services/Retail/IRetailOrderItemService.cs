using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IRetailOrderItemService : IGenericService<RetailOrderItem>
    {
        Task<IEnumerable<RetailOrderItemResponseDto>> GetAllDtosAsync();
        Task<RetailOrderItemResponseDto?> GetDtoByIdAsync(long id);
        Task<RetailOrderItemResponseDto> CreateDtoAsync(RetailOrderItemCreateDto itemDto);
        Task<RetailOrderItemResponseDto?> UpdateDtoAsync(long id, RetailOrderItemUpdateDto itemDto);
    }
}
