using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IRetailOrderService : IGenericService<RetailOrder>
    {
        List<RetailOrderResponseDto> MapToDtos(IEnumerable<RetailOrder> items);
        RetailOrderResponseDto MapToDto(RetailOrder item);
        RetailOrderResponseDto MapToDtoWithItems(RetailOrder item);
    }
}
