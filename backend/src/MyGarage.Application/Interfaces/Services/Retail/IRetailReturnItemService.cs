using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IRetailReturnItemService : IGenericService<RetailReturnItem>
    {
        List<RetailReturnItemResponseDto> MapToDtos(IEnumerable<RetailReturnItem> items);
        RetailReturnItemResponseDto MapToDto(RetailReturnItem item);
        Task<RetailReturnItemResponseDto> CreatePendingDtoAsync(RetailReturnItemCreateDto itemDto, long actorId);
    }
}
