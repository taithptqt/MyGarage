using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class CommissionRuleService : GenericService<CommissionRule>, ICommissionRuleService
    {
        private readonly IMapper _mapper;

        public CommissionRuleService(ICommissionRuleRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<CommissionRuleResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<CommissionRuleResponseDto>>(items);
        }

        public async Task<CommissionRuleResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<CommissionRuleResponseDto>(item);
        }

        public CommissionRuleResponseDto MapToDto(CommissionRule item)
            => _mapper.Map<CommissionRuleResponseDto>(item);

        public CommissionRule MapToEntity(CommissionRuleCreateDto itemDto)
            => _mapper.Map<CommissionRule>(itemDto);

        public void MapUpdate(CommissionRuleUpdateDto itemDto, CommissionRule existingItem)
            => _mapper.Map(itemDto, existingItem);
    }
}
