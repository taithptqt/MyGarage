using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class InventoryAuditDetailService : GenericService<InventoryAuditDetail>, IInventoryAuditDetailService
    {
        private readonly IMapper _mapper;

        public InventoryAuditDetailService(IInventoryAuditDetailRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<InventoryAuditDetailResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<InventoryAuditDetailResponseDto>>(items);
        }

        public async Task<InventoryAuditDetailResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<InventoryAuditDetailResponseDto>(item);
        }

        public async Task<InventoryAuditDetailResponseDto> CreateDtoAsync(InventoryAuditDetailCreateDto itemDto)
        {
            var entity = _mapper.Map<InventoryAuditDetail>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<InventoryAuditDetailResponseDto>(createdItem);
        }

        public async Task<InventoryAuditDetailResponseDto?> UpdateDtoAsync(long id, InventoryAuditDetailUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<InventoryAuditDetailResponseDto>(updatedItem);
        }
    }
}
