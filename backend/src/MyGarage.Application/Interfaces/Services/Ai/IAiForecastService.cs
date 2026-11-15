using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAiForecastService : IGenericService<AiForecast>
    {
        Task<AiForecastResponseDto?> GetDtoByIdAsync(long id);
        Task<AiForecastResponseDto> CreateDtoAsync(AiForecastCreateDto itemDto);
        Task<AiForecastResponseDto?> UpdateDtoAsync(long id, AiForecastUpdateDto itemDto);
    }
}
