using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InventoryService : GenericService<Inventory>, IInventoryService
    {
        private readonly IMapper _mapper;

        public InventoryService(IInventoryRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<InventoryResponseDto> MapToDtos(IEnumerable<Inventory> items)
        {
            return _mapper.Map<List<InventoryResponseDto>>(items);
        }

        public async Task<InventoryResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<InventoryResponseDto>(item);
        }

        public async Task<InventoryResponseDto> CreateDtoAsync(InventoryCreateDto itemDto)
        {
            var entity = _mapper.Map<Inventory>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<InventoryResponseDto>(createdItem);
        }

        public async Task<InventoryResponseDto?> UpdateDtoAsync(long id, InventoryUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<InventoryResponseDto>(updatedItem);
        }
    }
}
