using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class GatePassService : GenericService<GatePass>, IGatePassService
    {
        private readonly IMapper _mapper;

        public GatePassService(IGatePassRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<GatePassResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<GatePassResponseDto>(item);
        }

        public async Task<GatePassResponseDto> CreateDtoAsync(GatePassCreateDto itemDto)
        {
            var entity = _mapper.Map<GatePass>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<GatePassResponseDto>(createdItem);
        }

        public async Task<GatePassResponseDto?> UpdateDtoAsync(long id, GatePassUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            var originalServiceOrderId = existingItem.ServiceOrderId;
            _mapper.Map(itemDto, existingItem);
            if (itemDto.ServiceOrderId <= 0)
            {
                existingItem.ServiceOrderId = originalServiceOrderId;
            }

            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<GatePassResponseDto>(updatedItem);
        }
    }
}
