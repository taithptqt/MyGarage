using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ConversationService : GenericService<Conversation>, IConversationService
    {
        private readonly IMapper _mapper;

        public ConversationService(IConversationRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<ConversationResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<ConversationResponseDto>>(items);
        }

        public async Task<ConversationResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ConversationResponseDto>(item);
        }

        public async Task<ConversationResponseDto> CreateDtoAsync(ConversationCreateDto itemDto)
        {
            var entity = _mapper.Map<Conversation>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ConversationResponseDto>(createdItem);
        }

        public async Task<ConversationResponseDto?> UpdateDtoAsync(long id, ConversationUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ConversationResponseDto>(updatedItem);
        }
    }
}
