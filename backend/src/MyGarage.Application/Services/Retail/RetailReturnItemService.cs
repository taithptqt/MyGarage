using AutoMapper;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailReturnItemService : GenericService<RetailReturnItem>, IRetailReturnItemService
    {
        private readonly IMapper _mapper;

        public RetailReturnItemService(IRetailReturnItemRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<RetailReturnItemResponseDto> MapToDtos(IEnumerable<RetailReturnItem> items)
        {
            return _mapper.Map<List<RetailReturnItemResponseDto>>(items);
        }

        public RetailReturnItemResponseDto MapToDto(RetailReturnItem item)
        {
            return _mapper.Map<RetailReturnItemResponseDto>(item);
        }

        public async Task<RetailReturnItemResponseDto> CreatePendingDtoAsync(RetailReturnItemCreateDto itemDto, long actorId)
        {
            var entity = _mapper.Map<RetailReturnItem>(itemDto);
            entity.Status = "PENDING";
            entity.CreatedBy = actorId;
            entity.UpdatedBy = null;
            var createdItem = await AddAsync(entity);
            return _mapper.Map<RetailReturnItemResponseDto>(createdItem);
        }
    }
}
