using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPurchaseOrderItemService : IGenericService<PurchaseOrderItem>
    {
        List<PurchaseOrderItemResponseDto> MapToDtos(IEnumerable<PurchaseOrderItem> items);
        PurchaseOrderItemResponseDto MapToDto(PurchaseOrderItem item);
    }
}
