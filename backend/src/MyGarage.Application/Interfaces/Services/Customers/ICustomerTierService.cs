using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICustomerTierService : IGenericService<CustomerTier>
    {
        Task<IEnumerable<CustomerTierResponseDto>> GetAllDtosAsync();
        Task<CustomerTierResponseDto?> GetDtoByIdAsync(long id);
        Task<CustomerTierResponseDto> CreateDtoAsync(CustomerTierCreateDto itemDto);
        Task<CustomerTierResponseDto?> UpdateDtoAsync(long id, CustomerTierUpdateDto itemDto);
    }
}
