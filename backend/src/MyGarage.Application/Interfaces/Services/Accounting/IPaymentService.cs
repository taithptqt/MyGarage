using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IPaymentService : IGenericService<Payment>
    {
        List<PaymentResponseDto> MapToDtos(IEnumerable<Payment> items);
        PaymentResponseDto MapToDto(Payment item);
        Task<PaymentResponseDto?> GetDtoByIdAsync(long id);
        Payment MapToEntity(PaymentCreateDto itemDto);
        void MapUpdate(PaymentUpdateDto itemDto, Payment existingItem);
        Task<IEnumerable<Payment>> GetByCustomerPayerAsync(long customerId);
    }
}
