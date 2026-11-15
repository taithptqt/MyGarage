using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDeliveryService : IGenericService<Delivery>
    {
        IEnumerable<DeliveryResponseDto> MapToDtos(IEnumerable<Delivery> items);
        Task<DeliveryResponseDto?> GetDtoByIdAsync(long id);
        Task<DeliveryResponseDto> CreateDtoAsync(DeliveryCreateDto itemDto);
        Task<DeliveryResponseDto?> UpdateDtoAsync(long id, DeliveryUpdateDto itemDto);
    }
}
