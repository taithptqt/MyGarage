using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StaffScheduleService : GenericService<StaffSchedule>, IStaffScheduleService
    {
        private readonly IMapper _mapper;

        public StaffScheduleService(IStaffScheduleRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<StaffScheduleResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<StaffScheduleResponseDto>>(items);
        }

        public async Task<StaffScheduleResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<StaffScheduleResponseDto>(item);
        }

        public async Task<StaffScheduleResponseDto> CreateDtoAsync(StaffScheduleCreateDto itemDto)
        {
            var entity = _mapper.Map<StaffSchedule>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<StaffScheduleResponseDto>(createdItem);
        }

        public async Task<StaffScheduleResponseDto?> UpdateDtoAsync(long id, StaffScheduleUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<StaffScheduleResponseDto>(updatedItem);
        }
    }
}
