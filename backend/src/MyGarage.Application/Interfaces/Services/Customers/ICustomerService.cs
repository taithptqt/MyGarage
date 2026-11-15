using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICustomerService : IGenericService<Customer>
    {
        Task<IEnumerable<Customer>> GetByBranchIdAsync(long branchId);
        CustomerResponseDto MapToDto(Customer customer);
        IEnumerable<CustomerResponseDto> MapToDtos(IEnumerable<Customer> items);
        Task<CustomerResponseDto?> GetDtoByIdAsync(long id);
        Task<CustomerResponseDto> CreateDtoAsync(CustomerCreateDto customerDto, long? branchId = null);
        Task<CustomerResponseDto?> UpdateDtoAsync(long id, CustomerUpdateDto customerDto, long? branchId = null);
    }
}
