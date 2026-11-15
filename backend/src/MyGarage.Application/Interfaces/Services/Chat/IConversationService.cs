using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IConversationService : IGenericService<Conversation>
    {
        Task<IEnumerable<ConversationResponseDto>> GetAllDtosAsync();
        Task<ConversationResponseDto?> GetDtoByIdAsync(long id);
        Task<ConversationResponseDto> CreateDtoAsync(ConversationCreateDto itemDto);
        Task<ConversationResponseDto?> UpdateDtoAsync(long id, ConversationUpdateDto itemDto);
    }
}
