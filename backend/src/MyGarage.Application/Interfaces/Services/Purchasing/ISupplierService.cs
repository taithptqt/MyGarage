using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ISupplierService : IGenericService<Supplier>
    {
        Task<IEnumerable<SupplierResponseDto>> GetAllDtosAsync();
        List<SupplierResponseDto> MapToDtos(IEnumerable<Supplier> items);
        Task<SupplierResponseDto?> GetDtoByIdAsync(long id);
        Task<SupplierResponseDto> CreateDtoAsync(SupplierCreateDto itemDto);
        Task<SupplierResponseDto?> UpdateDtoAsync(long id, SupplierUpdateDto itemDto);
    }
}
