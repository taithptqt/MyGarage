using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IInvoiceService : IGenericService<Invoice>
    {
        Task<Invoice?> EnsureForPaymentContextAsync(long? serviceOrderId, long? retailOrderId, string? payerType, long? payerId);
        Task<InvoiceBackfillResponseDto> BackfillCompletedPaymentsAsync();
    }
}
