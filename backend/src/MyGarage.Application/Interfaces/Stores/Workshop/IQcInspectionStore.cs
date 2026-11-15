using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IQcInspectionStore
{
    Task<QcInspection?> GetInspectionForUpdateAsync(long id, CancellationToken ct = default);
    Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId, CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
