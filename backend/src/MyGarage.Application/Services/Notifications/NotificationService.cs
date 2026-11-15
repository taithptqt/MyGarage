using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class NotificationService : GenericService<Notification>, INotificationService
    {
        private readonly IMapper _mapper;

        public NotificationService(INotificationRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public NotificationResponseDto MapToDto(Notification item)
            => _mapper.Map<NotificationResponseDto>(item);

        public async Task<NotificationResponseDto> CreateDtoAsync(NotificationCreateDto itemDto)
        {
            var entity = _mapper.Map<Notification>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<NotificationResponseDto>(createdItem);
        }

        public void MapUpdate(NotificationUpdateDto itemDto, Notification existingItem)
            => _mapper.Map(itemDto, existingItem);
    }
}
