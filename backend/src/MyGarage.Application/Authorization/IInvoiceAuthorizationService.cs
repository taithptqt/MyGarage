using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IInvoiceAuthorizationService
{
    Task<bool> CanViewInvoicesAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanViewInvoiceAsync(long invoiceId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanCreateInvoiceAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<bool> CanExportTaxSummaryAsync(UserActorContext actor, CancellationToken ct = default);
}
