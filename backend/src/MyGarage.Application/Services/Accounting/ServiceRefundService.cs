using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ServiceRefundService : GenericService<ServiceRefund>, IServiceRefundService
    {
        private readonly IMapper _mapper;

        public ServiceRefundService(IServiceRefundRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<ServiceRefundResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<ServiceRefundResponseDto>>(items);
        }

        public async Task<ServiceRefundResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ServiceRefundResponseDto>(item);
        }

        public async Task<ServiceRefundResponseDto> CreateDtoAsync(ServiceRefundCreateDto itemDto, long actorUserId)
        {
            var entity = _mapper.Map<ServiceRefund>(itemDto);
            entity.Status = nameof(ServiceRefundStatusCode.PENDING);
            entity.CreatedBy = actorUserId;
            entity.UpdatedBy = actorUserId;
            entity.ApprovedBy = null;
            entity.ApprovedAt = null;
            entity.ApprovalNote = null;
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ServiceRefundResponseDto>(createdItem);
        }

        public async Task<ServiceRefundResponseDto?> UpdateDtoAsync(long id, ServiceRefundUpdateDto itemDto, long actorUserId)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            existingItem.UpdatedBy = actorUserId;
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ServiceRefundResponseDto>(updatedItem);
        }
    }
}
