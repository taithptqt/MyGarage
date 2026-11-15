using System;
using System.Threading.Tasks;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IServiceOrderTransactionCommandStore
{
    Task ExecuteInTransactionAsync(Func<Task> action);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<Task<TResult>> action, bool clearChangeTracker = false);
    Task SaveChangesAsync();
    Task SaveChangesInTransactionAsync();
}
