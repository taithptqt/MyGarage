using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ITeamService : IGenericService<Team>
    {
        IEnumerable<TeamResponseDto> MapToDtos(IEnumerable<Team> items);
        Task<TeamResponseDto?> GetDtoByIdAsync(long id);
        Task<TeamResponseDto> CreateDtoAsync(TeamCreateDto itemDto);
        Task<TeamResponseDto?> UpdateDtoAsync(long id, TeamUpdateDto itemDto);
    }
}
