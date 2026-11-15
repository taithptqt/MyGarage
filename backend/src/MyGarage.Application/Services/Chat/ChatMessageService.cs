using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ChatMessageService : GenericService<ChatMessage>, IChatMessageService
    {
        private readonly IMapper _mapper;

        public ChatMessageService(IChatMessageRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<ChatMessageResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<ChatMessageResponseDto>>(items);
        }

        public async Task<ChatMessageResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ChatMessageResponseDto>(item);
        }

        public async Task<ChatMessageResponseDto> CreateDtoAsync(ChatMessageCreateDto itemDto)
        {
            var entity = _mapper.Map<ChatMessage>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ChatMessageResponseDto>(createdItem);
        }

        public async Task<ChatMessageResponseDto?> UpdateDtoAsync(long id, ChatMessageUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ChatMessageResponseDto>(updatedItem);
        }
    }
}
