using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InventoryAuditService : GenericService<InventoryAudit>, IInventoryAuditService
    {
        private readonly IMapper _mapper;

        public InventoryAuditService(IInventoryAuditRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<InventoryAuditResponseDto> MapToDtos(IEnumerable<InventoryAudit> items)
        {
            return _mapper.Map<List<InventoryAuditResponseDto>>(items);
        }

        public async Task<InventoryAuditResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<InventoryAuditResponseDto>(item);
        }

        public async Task<InventoryAuditResponseDto> CreateDtoAsync(InventoryAuditCreateDto itemDto)
        {
            var entity = _mapper.Map<InventoryAudit>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<InventoryAuditResponseDto>(createdItem);
        }

        public async Task<InventoryAuditResponseDto?> UpdateDtoAsync(long id, InventoryAuditUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<InventoryAuditResponseDto>(updatedItem);
        }
    }
}
