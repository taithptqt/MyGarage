using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceOrderSnapshotService : IGenericService<ServiceOrderSnapshot>
    {
        Task<IEnumerable<ServiceOrderSnapshotResponseDto>> GetAllDtosAsync();
        Task<ServiceOrderSnapshotResponseDto?> GetDtoByIdAsync(long id);
        Task<ServiceOrderSnapshotResponseDto> CreateDtoAsync(ServiceOrderSnapshotCreateDto itemDto);
        Task<ServiceOrderSnapshotResponseDto?> UpdateDtoAsync(long id, ServiceOrderSnapshotUpdateDto itemDto);
    }
}
