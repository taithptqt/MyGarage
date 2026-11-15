using AutoMapper;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class PurchaseOrderItemService : GenericService<PurchaseOrderItem>, IPurchaseOrderItemService
    {
        private readonly IMapper _mapper;

        public PurchaseOrderItemService(IPurchaseOrderItemRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<PurchaseOrderItemResponseDto> MapToDtos(IEnumerable<PurchaseOrderItem> items)
        {
            return _mapper.Map<List<PurchaseOrderItemResponseDto>>(items);
        }

        public PurchaseOrderItemResponseDto MapToDto(PurchaseOrderItem item)
        {
            return _mapper.Map<PurchaseOrderItemResponseDto>(item);
        }
    }
}
