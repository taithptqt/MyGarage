using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IMaterialRequestDetailService : IGenericService<MaterialRequestDetail>
    {
        Task<IEnumerable<MaterialRequestDetailResponseDto>> GetAllDtosAsync();
        List<MaterialRequestDetailResponseDto> MapToDtos(IEnumerable<MaterialRequestDetail> items);
        Task<MaterialRequestDetailResponseDto?> GetDtoByIdAsync(long id);
        Task<MaterialRequestDetailResponseDto> CreateDtoAsync(MaterialRequestDetailCreateDto itemDto);
        Task<MaterialRequestDetailResponseDto?> UpdateDtoAsync(long id, MaterialRequestDetailUpdateDto itemDto, MaterialRequestDetail? existingItem = null);
    }
}
