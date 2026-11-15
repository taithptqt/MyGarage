using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IProductImageService : IGenericService<ProductImage>
    {
        Task<IEnumerable<ProductImageResponseDto>> GetAllDtosAsync(long? productId = null);
        Task<ProductImageResponseDto?> GetDtoByIdAsync(long id);
        Task<ProductImageResponseDto> CreateDtoAsync(ProductImageCreateDto itemDto);
        Task<ProductImageResponseDto?> UpdateDtoAsync(long id, ProductImageUpdateDto itemDto);
    }
}
