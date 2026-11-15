using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICustomerVoucherService : IGenericService<CustomerVoucher>
    {
        IEnumerable<CustomerVoucherResponseDto> MapToDtos(IEnumerable<CustomerVoucher> items);
        Task<CustomerVoucherResponseDto?> GetDtoByIdAsync(long id);
        Task<CustomerVoucherResponseDto> CreateDtoAsync(CustomerVoucherCreateDto itemDto);
        Task<CustomerVoucherResponseDto?> UpdateDtoAsync(long id, CustomerVoucherUpdateDto itemDto);
    }
}
