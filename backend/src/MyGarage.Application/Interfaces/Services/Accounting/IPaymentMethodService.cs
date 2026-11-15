using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPaymentMethodService : IGenericService<PaymentMethod>
    {
        Task<IEnumerable<PaymentMethodResponseDto>> GetAllDtosAsync();
        Task<PaymentMethodResponseDto?> GetDtoByIdAsync(long id);
        Task<PaymentMethodResponseDto> CreateDtoAsync(PaymentMethodCreateDto itemDto);
        Task<PaymentMethodResponseDto?> UpdateDtoAsync(long id, PaymentMethodUpdateDto itemDto);
    }
}
