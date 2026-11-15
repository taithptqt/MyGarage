using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Purchasing;

public interface IPurchaseOrderTransactionStore
{
    bool HasActiveTransaction();

    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);

    Task<TResult> ExecuteSerializableReceiveAsync<TResult>(
        long purchaseOrderId,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
