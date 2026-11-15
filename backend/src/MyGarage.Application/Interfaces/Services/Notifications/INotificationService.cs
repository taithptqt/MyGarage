using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface INotificationService : IGenericService<Notification>
    {
        NotificationResponseDto MapToDto(Notification item);
        Task<NotificationResponseDto> CreateDtoAsync(NotificationCreateDto itemDto);
        void MapUpdate(NotificationUpdateDto itemDto, Notification existingItem);
    }
}
