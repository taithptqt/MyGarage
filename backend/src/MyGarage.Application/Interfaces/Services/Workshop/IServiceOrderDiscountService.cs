using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceOrderDiscountService : IGenericService<ServiceOrderDiscount>
    {
        Task<IEnumerable<ServiceOrderDiscountResponseDto>> GetAllDtosAsync();
        Task<ServiceOrderDiscountResponseDto?> GetDtoByIdAsync(long id);
        Task<ServiceOrderDiscountResponseDto> CreateDtoAsync(ServiceOrderDiscountCreateDto itemDto);
        Task<ServiceOrderDiscountResponseDto?> UpdateDtoAsync(long id, ServiceOrderDiscount existingItem, ServiceOrderDiscountUpdateDto itemDto);
    }
}
