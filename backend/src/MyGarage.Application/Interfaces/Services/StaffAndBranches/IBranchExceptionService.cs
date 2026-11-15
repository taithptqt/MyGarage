using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IBranchExceptionService : IGenericService<BranchException>
    {
        Task<IEnumerable<BranchExceptionResponseDto>> GetAllDtosAsync();
        Task<BranchExceptionResponseDto?> GetDtoByIdAsync(long id);
        Task<BranchExceptionResponseDto> CreateDtoAsync(BranchExceptionCreateDto itemDto);
        Task<BranchExceptionResponseDto?> UpdateDtoAsync(long id, BranchExceptionUpdateDto itemDto);
    }
}
