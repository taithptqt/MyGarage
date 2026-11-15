using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICategoryService : IGenericService<Category>
    {
        Task<IEnumerable<CategoryResponseDto>> GetAllDtosAsync();
        List<CategoryResponseDto> MapToDtos(IEnumerable<Category> items);
        Task<CategoryResponseDto?> GetDtoByIdAsync(long id);
        Task<CategoryResponseDto> CreateDtoAsync(CategoryCreateDto itemDto);
        Task<CategoryResponseDto?> UpdateDtoAsync(long id, CategoryUpdateDto itemDto);
    }
}
