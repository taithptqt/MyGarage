using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiModelConfigService : GenericService<AiModelConfig>, IAiModelConfigService
    {
        private readonly IMapper _mapper;

        public AiModelConfigService(IAiModelConfigRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<AiModelConfigResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<AiModelConfigResponseDto>>(items);
        }

        public async Task<AiModelConfigResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<AiModelConfigResponseDto>(item);
        }

        public async Task<AiModelConfigResponseDto> CreateDtoAsync(AiModelConfigCreateDto itemDto)
        {
            var entity = _mapper.Map<AiModelConfig>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<AiModelConfigResponseDto>(createdItem);
        }

        public async Task<AiModelConfigResponseDto?> UpdateDtoAsync(long id, AiModelConfigUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<AiModelConfigResponseDto>(updatedItem);
        }
    }
}
