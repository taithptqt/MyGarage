using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IRetailReturnService : IGenericService<RetailReturn>
    {
        List<RetailReturnResponseDto> MapToDtos(IEnumerable<RetailReturn> items);
        RetailReturnResponseDto MapToDto(RetailReturn item);
        Task<RetailReturnResponseDto> CreatePendingDtoAsync(RetailReturnCreateDto itemDto, long actorId);
    }
}
