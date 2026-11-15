using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class CategoryService : GenericService<Category>, ICategoryService
    {
        private readonly IMapper _mapper;

        public CategoryService(ICategoryRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<CategoryResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<CategoryResponseDto>>(items);
        }

        public List<CategoryResponseDto> MapToDtos(IEnumerable<Category> items)
        {
            return _mapper.Map<List<CategoryResponseDto>>(items);
        }

        public async Task<CategoryResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<CategoryResponseDto>(item);
        }

        public async Task<CategoryResponseDto> CreateDtoAsync(CategoryCreateDto itemDto)
        {
            var entity = _mapper.Map<Category>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<CategoryResponseDto>(createdItem);
        }

        public async Task<CategoryResponseDto?> UpdateDtoAsync(long id, CategoryUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<CategoryResponseDto>(updatedItem);
        }
    }
}
