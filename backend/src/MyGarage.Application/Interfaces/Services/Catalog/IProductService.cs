using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IProductService : IGenericService<Product>
    {
        List<ProductResponseDto> MapToDtos(IEnumerable<Product> items);
        List<ProductImageResponseDto> MapImagesToDtos(IEnumerable<ProductImage> images);
        Task<ProductResponseDto?> GetDtoByIdAsync(long id);
        Task<ProductResponseDto> CreateDtoAsync(ProductCreateDto itemDto);
        Task<ProductResponseDto?> UpdateDtoAsync(long id, ProductUpdateDto itemDto);
    }
}
