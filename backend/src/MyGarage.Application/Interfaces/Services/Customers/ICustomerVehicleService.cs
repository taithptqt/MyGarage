using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICustomerVehicleService : IGenericService<CustomerVehicle>
    {
        IEnumerable<CustomerVehicleResponseDto> MapToDtos(IEnumerable<CustomerVehicle> items);
        Task<CustomerVehicleResponseDto?> GetDtoByIdAsync(long id);
        Task<CustomerVehicleResponseDto> CreateDtoAsync(CustomerVehicleCreateDto itemDto, long? overrideCustomerId = null);
        Task<CustomerVehicleResponseDto?> UpdateDtoAsync(long id, CustomerVehicleUpdateDto itemDto, bool preserveCustomerId = false);
    }
}
