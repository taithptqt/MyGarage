using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IMaterialReturnService : IGenericService<MaterialReturn>
    {
        Task<MaterialReturn> ProcessReturnAsync(MaterialReturnCreateDto dto);
        List<MaterialReturnResponseDto> MapToDtos(IEnumerable<MaterialReturn> items);
        Task<MaterialReturnResponseDto?> GetDtoByIdAsync(long id);
        Task<MaterialReturnResponseDto> ProcessReturnDtoAsync(MaterialReturnCreateDto dto);
        Task<MaterialReturnResponseDto?> UpdateDtoAsync(long id, MaterialReturnUpdateDto itemDto, MaterialReturn? existingItem = null);
    }
}
