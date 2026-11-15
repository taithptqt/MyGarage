using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceCategoryService : GenericService<ServiceCategory>, IServiceCategoryService
    {
        private readonly IMapper _mapper;

        public ServiceCategoryService(IServiceCategoryRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<ServiceCategoryResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<ServiceCategoryResponseDto>>(items);
        }

        public async Task<ServiceCategoryResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ServiceCategoryResponseDto>(item);
        }

        public async Task<ServiceCategoryResponseDto> CreateDtoAsync(ServiceCategoryCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceCategory>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ServiceCategoryResponseDto>(createdItem);
        }

        public async Task<ServiceCategoryResponseDto?> UpdateDtoAsync(long id, ServiceCategoryUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ServiceCategoryResponseDto>(updatedItem);
        }
    }
}
