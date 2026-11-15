using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ShiftClosingService : GenericService<ShiftClosing>, IShiftClosingService
    {
        private readonly IMapper _mapper;

        public ShiftClosingService(IShiftClosingRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<ShiftClosingResponseDto> MapToDtos(IEnumerable<ShiftClosing> items)
            => _mapper.Map<List<ShiftClosingResponseDto>>(items);

        public async Task<ShiftClosingResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<ShiftClosingResponseDto>(item);
        }

        public async Task<ShiftClosingResponseDto> CreateDtoAsync(ShiftClosingCreateDto itemDto)
        {
            var entity = _mapper.Map<ShiftClosing>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<ShiftClosingResponseDto>(createdItem);
        }

        public async Task<ShiftClosingResponseDto?> UpdateDtoAsync(long id, ShiftClosingUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<ShiftClosingResponseDto>(updatedItem);
        }
    }
}
