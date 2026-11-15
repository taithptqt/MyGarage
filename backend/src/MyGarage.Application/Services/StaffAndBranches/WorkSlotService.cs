using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class WorkSlotService : GenericService<WorkSlot>, IWorkSlotService
    {
        private readonly IMapper _mapper;

        public WorkSlotService(IWorkSlotRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<WorkSlotResponseDto> MapToDtos(IEnumerable<WorkSlot> items)
        {
            return _mapper.Map<List<WorkSlotResponseDto>>(items);
        }

        public async Task<WorkSlotResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<WorkSlotResponseDto>(item);
        }

        public async Task<WorkSlotResponseDto> CreateDtoAsync(WorkSlotCreateDto itemDto)
        {
            var entity = _mapper.Map<WorkSlot>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<WorkSlotResponseDto>(createdItem);
        }

        public async Task<WorkSlotResponseDto?> UpdateDtoAsync(long id, WorkSlotUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<WorkSlotResponseDto>(updatedItem);
        }
    }
}
