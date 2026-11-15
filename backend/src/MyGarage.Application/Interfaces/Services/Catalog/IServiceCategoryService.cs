using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceCategoryService : IGenericService<ServiceCategory>
    {
        Task<IEnumerable<ServiceCategoryResponseDto>> GetAllDtosAsync();
        Task<ServiceCategoryResponseDto?> GetDtoByIdAsync(long id);
        Task<ServiceCategoryResponseDto> CreateDtoAsync(ServiceCategoryCreateDto itemDto);
        Task<ServiceCategoryResponseDto?> UpdateDtoAsync(long id, ServiceCategoryUpdateDto itemDto);
    }
}
