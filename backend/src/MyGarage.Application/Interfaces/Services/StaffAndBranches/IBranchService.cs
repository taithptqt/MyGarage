using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IBranchService : IGenericService<Branch>
    {
        Task<IEnumerable<BranchResponseDto>> GetAllDtosAsync();
        List<BranchResponseDto> MapToDtos(IEnumerable<Branch> items);
        Task<BranchResponseDto?> GetDtoByIdAsync(long id);
        Task<BranchResponseDto> CreateDtoAsync(BranchCreateDto itemDto);
        Task<BranchResponseDto?> UpdateDtoAsync(long id, BranchUpdateDto itemDto);
    }
}
