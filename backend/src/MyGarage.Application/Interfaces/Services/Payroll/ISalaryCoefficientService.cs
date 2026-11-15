using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ISalaryCoefficientService : IGenericService<SalaryCoefficient>
    {
        Task<IEnumerable<SalaryCoefficientResponseDto>> GetAllDtosAsync();
        Task<SalaryCoefficientResponseDto?> GetDtoByIdAsync(long id);
        Task<SalaryCoefficientResponseDto> CreateDtoAsync(SalaryCoefficientCreateDto itemDto);
        Task<SalaryCoefficientResponseDto?> UpdateDtoAsync(long id, SalaryCoefficientUpdateDto itemDto);
    }
}
