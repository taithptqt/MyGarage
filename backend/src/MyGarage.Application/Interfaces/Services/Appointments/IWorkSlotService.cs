using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IWorkSlotService : IGenericService<WorkSlot>
    {
        List<WorkSlotResponseDto> MapToDtos(IEnumerable<WorkSlot> items);
        Task<WorkSlotResponseDto?> GetDtoByIdAsync(long id);
        Task<WorkSlotResponseDto> CreateDtoAsync(WorkSlotCreateDto itemDto);
        Task<WorkSlotResponseDto?> UpdateDtoAsync(long id, WorkSlotUpdateDto itemDto);
    }
}
