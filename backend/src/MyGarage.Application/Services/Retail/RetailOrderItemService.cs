using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailOrderItemService : GenericService<RetailOrderItem>, IRetailOrderItemService
    {
        private readonly IMapper _mapper;

        public RetailOrderItemService(IRetailOrderItemRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public async Task<IEnumerable<RetailOrderItemResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<RetailOrderItemResponseDto>>(items);
        }

        public async Task<RetailOrderItemResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<RetailOrderItemResponseDto>(item);
        }

        public async Task<RetailOrderItemResponseDto> CreateDtoAsync(RetailOrderItemCreateDto itemDto)
        {
            var entity = _mapper.Map<RetailOrderItem>(itemDto);
            var createdItem = await AddAsync(entity);
            return _mapper.Map<RetailOrderItemResponseDto>(createdItem);
        }

        public async Task<RetailOrderItemResponseDto?> UpdateDtoAsync(long id, RetailOrderItemUpdateDto itemDto)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return null;
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await UpdateAsync(id, existingItem);
            return updatedItem == null ? null : _mapper.Map<RetailOrderItemResponseDto>(updatedItem);
        }
    }
}
