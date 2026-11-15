using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInventoryAuditDetailService : IGenericService<InventoryAuditDetail>
    {
        Task<IEnumerable<InventoryAuditDetailResponseDto>> GetAllDtosAsync();
        Task<InventoryAuditDetailResponseDto?> GetDtoByIdAsync(long id);
        Task<InventoryAuditDetailResponseDto> CreateDtoAsync(InventoryAuditDetailCreateDto itemDto);
        Task<InventoryAuditDetailResponseDto?> UpdateDtoAsync(long id, InventoryAuditDetailUpdateDto itemDto);
    }
}
