using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ProductService : GenericService<Product>, IProductService
    {
        private readonly IMapper _mapper;

        public ProductService(IProductRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<ProductResponseDto> MapToDtos(IEnumerable<Product> items)
        {
            return _mapper.Map<List<ProductResponseDto>>(items);
        }

        public List<ProductImageResponseDto> MapImagesToDtos(IEnumerable<ProductImage> images)
        {
            return _mapper.Map<List<ProductImageResponseDto>>(images);
        }

        public async Task<ProductResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ProductResponseDto>(item);
        }

        public async Task<ProductResponseDto> CreateDtoAsync(ProductCreateDto itemDto)
        {
            var entity = _mapper.Map<Product>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ProductResponseDto>(createdItem);
        }

        public async Task<ProductResponseDto?> UpdateDtoAsync(long id, ProductUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ProductResponseDto>(updatedItem);
        }
    }
}
