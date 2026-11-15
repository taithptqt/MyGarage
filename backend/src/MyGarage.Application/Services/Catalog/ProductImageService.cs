using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ProductImageService : GenericService<ProductImage>, IProductImageService
    {
        private readonly IMapper _mapper;

        public ProductImageService(IProductImageRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<ProductImageResponseDto>> GetAllDtosAsync(long? productId = null)
        {
            var items = await GetAllAsync();
            if (productId.HasValue)
            {
                items = items.Where(item => item.ProductId == productId.Value).ToList();
            }

            return _mapper.Map<IEnumerable<ProductImageResponseDto>>(items);
        }

        public async Task<ProductImageResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ProductImageResponseDto>(item);
        }

        public async Task<ProductImageResponseDto> CreateDtoAsync(ProductImageCreateDto itemDto)
        {
            var entity = _mapper.Map<ProductImage>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ProductImageResponseDto>(createdItem);
        }

        public async Task<ProductImageResponseDto?> UpdateDtoAsync(long id, ProductImageUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ProductImageResponseDto>(updatedItem);
        }
    }
}
