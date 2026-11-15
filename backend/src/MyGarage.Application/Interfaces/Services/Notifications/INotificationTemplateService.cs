using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface INotificationTemplateService : IGenericService<NotificationTemplate>
    {
        Task<IEnumerable<NotificationTemplateResponseDto>> GetAllDtosAsync();
        Task<NotificationTemplateResponseDto?> GetDtoByIdAsync(long id);
        Task<NotificationTemplateResponseDto> CreateDtoAsync(NotificationTemplateCreateDto itemDto);
        Task<NotificationTemplateResponseDto?> UpdateDtoAsync(long id, NotificationTemplateUpdateDto itemDto);
    }
}
