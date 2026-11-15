using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInvoiceService : IGenericService<Invoice>
    {
        List<InvoiceResponseDto> MapToDtos(IEnumerable<Invoice> items);
        InvoiceResponseDto MapToDto(Invoice item);
        Task<InvoiceResponseDto?> GetDtoByIdAsync(long id);
        Task<Invoice?> EnsureForPaymentContextAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId);
        Task<(Invoice? Invoice, bool Created)> EnsureForPaymentContextWithOutcomeAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId);
        Task<InvoiceBackfillResponseDto> BackfillCompletedPaymentsAsync();
    }
}
