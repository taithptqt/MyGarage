using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ApprovalRequestService : GenericService<ApprovalRequest>, IApprovalRequestService
    {
        private readonly IMapper _mapper;

        public ApprovalRequestService(IApprovalRequestRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<ApprovalRequestResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ApprovalRequestResponseDto>(item);
        }

        public async Task<ApprovalRequestResponseDto> CreateDtoAsync(ApprovalRequestCreateDto itemDto)
        {
            var entity = _mapper.Map<ApprovalRequest>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ApprovalRequestResponseDto>(createdItem);
        }

        public async Task<ApprovalRequestResponseDto?> UpdateDtoAsync(long id, ApprovalRequestUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ApprovalRequestResponseDto>(updatedItem);
        }
    }
}
