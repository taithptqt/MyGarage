using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInventoryAuditService : IGenericService<InventoryAudit>
    {
        List<InventoryAuditResponseDto> MapToDtos(IEnumerable<InventoryAudit> items);
        Task<InventoryAuditResponseDto?> GetDtoByIdAsync(long id);
        Task<InventoryAuditResponseDto> CreateDtoAsync(InventoryAuditCreateDto itemDto);
        Task<InventoryAuditResponseDto?> UpdateDtoAsync(long id, InventoryAuditUpdateDto itemDto);
    }
}
