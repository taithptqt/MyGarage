using MyGarage.Models;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IQcInspectionService : IGenericService<QcInspection>
    {
        List<QcInspectionResponseDto> MapToDtos(IEnumerable<QcInspection> items);
        Task<QcInspectionResponseDto?> GetDtoByIdAsync(long id);
        Task<QcInspectionResponseDto> CreateDtoAsync(QcInspectionCreateDto itemDto);
        Task<QcInspectionResponseDto?> UpdateDtoAsync(long id, QcInspectionUpdateDto itemDto);
        Task<bool> PassAsync(long id, QcPassRequestDto request);
        Task<bool> FailAsync(long id, QcFailRequestDto request);
    }
}
