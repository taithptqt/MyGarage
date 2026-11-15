using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class ShiftService : GenericService<Shift>, IShiftService
    {
        private readonly IMapper _mapper;

        public ShiftService(IShiftRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<ShiftResponseDto> MapToDtos(IEnumerable<Shift> items)
        {
            return _mapper.Map<List<ShiftResponseDto>>(items);
        }

        public async Task<ShiftResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ShiftResponseDto>(item);
        }

        public async Task<ShiftResponseDto> CreateDtoAsync(ShiftCreateDto itemDto)
        {
            var entity = _mapper.Map<Shift>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ShiftResponseDto>(createdItem);
        }

        public async Task<ShiftResponseDto?> UpdateDtoAsync(long id, ShiftUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ShiftResponseDto>(updatedItem);
        }
    }
}
