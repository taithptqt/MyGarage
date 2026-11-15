using AutoMapper;
using MyGarage.DTOs.Responses;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class RetailOrderService : GenericService<RetailOrder>, IRetailOrderService
    {
        private readonly IMapper _mapper;

        public RetailOrderService(IRetailOrderRepository repository, IMapper mapper) : base(repository)
        {
            _mapper = mapper;
        }

        public List<RetailOrderResponseDto> MapToDtos(IEnumerable<RetailOrder> items)
        {
            return _mapper.Map<List<RetailOrderResponseDto>>(items);
        }

        public RetailOrderResponseDto MapToDto(RetailOrder item)
        {
            return _mapper.Map<RetailOrderResponseDto>(item);
        }

        public RetailOrderResponseDto MapToDtoWithItems(RetailOrder item)
        {
            var dto = _mapper.Map<RetailOrderResponseDto>(item);
            dto.Items = _mapper.Map<List<RetailOrderItemResponseDto>>(
                item.RetailOrderItems
                    .OrderByDescending(orderItem => orderItem.Id)
                    .ToList());
            return dto;
        }
    }
}
