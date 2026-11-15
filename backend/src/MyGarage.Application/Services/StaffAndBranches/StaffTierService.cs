using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class StaffTierService : GenericService<StaffTier>, IStaffTierService
    {
        private readonly IMapper _mapper;

        public StaffTierService(IStaffTierRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<StaffTierResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<StaffTierResponseDto>>(items);
        }

        public async Task<StaffTierResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<StaffTierResponseDto>(item);
        }

        public async Task<StaffTierResponseDto> CreateDtoAsync(StaffTierCreateDto itemDto)
        {
            var entity = _mapper.Map<StaffTier>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<StaffTierResponseDto>(createdItem);
        }

        public async Task<StaffTierResponseDto?> UpdateDtoAsync(long id, StaffTierUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<StaffTierResponseDto>(updatedItem);
        }
    }
}
