using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class BranchOperatingHourService : GenericService<BranchOperatingHour>, IBranchOperatingHourService
    {
        private readonly IMapper _mapper;

        public BranchOperatingHourService(IBranchOperatingHourRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<BranchOperatingHourResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<BranchOperatingHourResponseDto>>(items);
        }

        public async Task<BranchOperatingHourResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<BranchOperatingHourResponseDto>(item);
        }

        public async Task<BranchOperatingHourResponseDto> CreateDtoAsync(BranchOperatingHourCreateDto itemDto)
        {
            var entity = _mapper.Map<BranchOperatingHour>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<BranchOperatingHourResponseDto>(createdItem);
        }

        public async Task<BranchOperatingHourResponseDto?> UpdateDtoAsync(long id, BranchOperatingHourUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<BranchOperatingHourResponseDto>(updatedItem);
        }
    }
}
