using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IChatMessageService : IGenericService<ChatMessage>
    {
        Task<IEnumerable<ChatMessageResponseDto>> GetAllDtosAsync();
        Task<ChatMessageResponseDto?> GetDtoByIdAsync(long id);
        Task<ChatMessageResponseDto> CreateDtoAsync(ChatMessageCreateDto itemDto);
        Task<ChatMessageResponseDto?> UpdateDtoAsync(long id, ChatMessageUpdateDto itemDto);
    }
}
