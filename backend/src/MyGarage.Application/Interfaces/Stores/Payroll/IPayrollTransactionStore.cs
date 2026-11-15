using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Payroll;

public interface IPayrollTransactionStore
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
