using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IBayService : IGenericService<Bay>
    {
        Task<IEnumerable<BayResponseDto>> GetAllDtosAsync();
        Task<BayResponseDto?> GetDtoByIdAsync(long id);
        Task<BayResponseDto> CreateDtoAsync(BayCreateDto itemDto);
        Task<BayResponseDto?> UpdateDtoAsync(long id, BayUpdateDto itemDto);
    }
}
