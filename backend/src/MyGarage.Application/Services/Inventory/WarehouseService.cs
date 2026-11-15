using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class WarehouseService : GenericService<Warehouse>, IWarehouseService
    {
        private readonly IMapper _mapper;

        public WarehouseService(IWarehouseRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<WarehouseResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<WarehouseResponseDto>>(items);
        }

        public async Task<WarehouseResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<WarehouseResponseDto>(item);
        }

        public async Task<WarehouseResponseDto> CreateDtoAsync(WarehouseCreateDto itemDto)
        {
            var entity = _mapper.Map<Warehouse>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<WarehouseResponseDto>(createdItem);
        }

        public async Task<WarehouseResponseDto?> UpdateDtoAsync(long id, WarehouseUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<WarehouseResponseDto>(updatedItem);
        }
    }
}
