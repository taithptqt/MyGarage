using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInsuranceCompanyService : IGenericService<InsuranceCompany>
    {
        Task<IEnumerable<InsuranceCompanyResponseDto>> GetAllDtosAsync();
        Task<InsuranceCompanyResponseDto?> GetDtoByIdAsync(long id);
        Task<InsuranceCompanyResponseDto> CreateDtoAsync(InsuranceCompanyCreateDto itemDto);
        Task<InsuranceCompanyResponseDto?> UpdateDtoAsync(long id, InsuranceCompanyUpdateDto itemDto);
    }
}
