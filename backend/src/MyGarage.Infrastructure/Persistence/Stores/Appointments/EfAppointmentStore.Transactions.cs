using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed partial class EfAppointmentStore
{
    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await action();
                if (tx != null)
                {
                    await tx.CommitAsync();
                }
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync();
                }

                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                var result = await action();
                if (tx != null)
                {
                    await tx.CommitAsync();
                }

                return result;
            }
            catch
            {
                if (tx != null)
                {
                    await tx.RollbackAsync();
                }

                throw;
            }
        });
    }

    public async Task SaveChangesInTransactionAsync()
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync();
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }
}
