using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class NotificationTemplateService : GenericService<NotificationTemplate>, INotificationTemplateService
    {
        private readonly IMapper _mapper;

        public NotificationTemplateService(INotificationTemplateRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<NotificationTemplateResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<NotificationTemplateResponseDto>>(items);
        }

        public async Task<NotificationTemplateResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<NotificationTemplateResponseDto>(item);
        }

        public async Task<NotificationTemplateResponseDto> CreateDtoAsync(NotificationTemplateCreateDto itemDto)
        {
            var entity = _mapper.Map<NotificationTemplate>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<NotificationTemplateResponseDto>(createdItem);
        }

        public async Task<NotificationTemplateResponseDto?> UpdateDtoAsync(long id, NotificationTemplateUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<NotificationTemplateResponseDto>(updatedItem);
        }
    }
}
