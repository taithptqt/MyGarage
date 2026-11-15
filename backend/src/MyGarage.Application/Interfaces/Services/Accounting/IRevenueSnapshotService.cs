using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IRevenueSnapshotService : IGenericService<RevenueSnapshot>
    {
        Task<IEnumerable<RevenueSnapshotResponseDto>> GetAllDtosAsync();
        Task<RevenueSnapshotResponseDto?> GetDtoByIdAsync(long id);
        Task<RevenueSnapshotResponseDto> CreateDtoAsync(RevenueSnapshotCreateDto itemDto);
        Task<RevenueSnapshotResponseDto?> UpdateDtoAsync(long id, RevenueSnapshotUpdateDto itemDto);
    }
}
