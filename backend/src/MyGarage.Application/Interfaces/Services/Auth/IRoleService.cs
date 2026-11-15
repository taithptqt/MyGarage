using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IRoleService : IGenericService<Role>
    {
        Task<IEnumerable<RoleResponseDto>> GetAllDtosAsync();
        Task<RoleResponseDto?> GetDtoByIdAsync(long id);
        Task<RoleResponseDto> CreateDtoAsync(RoleCreateDto itemDto);
        Task<RoleResponseDto?> UpdateDtoAsync(long id, RoleUpdateDto itemDto);
    }
}
