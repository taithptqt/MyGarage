using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Vouchers;

public interface IVoucherRedemptionStore
{
    Task<TResult> ExecuteVoucherTransactionAsync<TResult>(
        string rejectionMessageWhenInTransaction,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken ct = default);

    Task AcquireVoucherOperationLockAsync(string name, CancellationToken ct = default);

    Task<long?> FindVoucherIdAsync(long? voucherId, string? voucherCode, CancellationToken ct = default);

    Task<Voucher> GetVoucherByIdRequiredAsync(long id, CancellationToken ct = default);

    Task AddRedemptionAndSaveAsync(VoucherRedemption redemption, CancellationToken ct = default);

    Task<long?> GetRedemptionVoucherIdAsync(long redemptionId, CancellationToken ct = default);

    Task<VoucherRedemption?> GetRedemptionWithVoucherForUpdateAsync(long redemptionId, CancellationToken ct = default);
}
