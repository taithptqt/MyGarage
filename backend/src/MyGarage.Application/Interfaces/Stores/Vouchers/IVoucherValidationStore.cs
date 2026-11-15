using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Vouchers;

public interface IVoucherValidationStore
{
    Task<Voucher?> ResolveVoucherForValidationAsync(long? voucherId, string? voucherCode, CancellationToken ct = default);

    Task<Invoice?> GetInvoiceByIdAsync(long invoiceId, bool track = false, CancellationToken ct = default);

    Task<IReadOnlyList<VoucherAssignment>> GetBranchAssignmentsAsync(long voucherId, CancellationToken ct = default);

    Task<string?> GetPaymentMethodCodeAsync(long paymentMethodId, CancellationToken ct = default);

    Task<int> CountCustomerRedeemedUsesAsync(long voucherId, long customerId, CancellationToken ct = default);

    Task<int> CountVehicleRedeemedUsesAsync(long voucherId, long vehicleId, CancellationToken ct = default);

    Task<bool> HasCustomerPriorOrderAsync(long customerId, long? excludeServiceOrderId, long? excludeRetailOrderId, CancellationToken ct = default);
}
