using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Invoices.DTOs;

namespace MyGarage.Application.Invoices;

public interface IInvoiceApplicationService
{
    Task<InvoiceDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<InvoiceDto> CreateInvoiceAsync(InvoiceCreateInput input, UserActorContext actor, CancellationToken ct = default);
    Task<InvoiceCalculationResultDto> CalculateTotalsAsync(long invoiceId, CancellationToken ct = default);
    Task<long?> GetServiceOrderIdByInvoiceIdAsync(long invoiceId, CancellationToken ct = default);
}
