using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AiKnowledgeBaseService : GenericService<AiKnowledgeBase>, IAiKnowledgeBaseService
    {
        private readonly IMapper _mapper;

        public AiKnowledgeBaseService(IAiKnowledgeBaseRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<AiKnowledgeBaseResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<AiKnowledgeBaseResponseDto>>(items);
        }

        public async Task<AiKnowledgeBaseResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<AiKnowledgeBaseResponseDto>(item);
        }

        public async Task<AiKnowledgeBaseResponseDto> CreateDtoAsync(AiKnowledgeBaseCreateDto itemDto)
        {
            var entity = _mapper.Map<AiKnowledgeBase>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<AiKnowledgeBaseResponseDto>(createdItem);
        }

        public async Task<AiKnowledgeBaseResponseDto?> UpdateDtoAsync(long id, AiKnowledgeBaseUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<AiKnowledgeBaseResponseDto>(updatedItem);
        }
    }
}
