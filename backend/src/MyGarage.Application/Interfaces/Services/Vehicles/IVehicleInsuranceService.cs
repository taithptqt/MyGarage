using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVehicleInsuranceService : IGenericService<VehicleInsurance>
    {
        Task<IEnumerable<VehicleInsuranceResponseDto>> GetAllDtosAsync();
        Task<VehicleInsuranceResponseDto?> GetDtoByIdAsync(long id);
        Task<VehicleInsuranceResponseDto> CreateDtoAsync(VehicleInsuranceCreateDto itemDto);
        Task<VehicleInsuranceResponseDto?> UpdateDtoAsync(long id, VehicleInsuranceUpdateDto itemDto);
    }
}
