using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services;

internal sealed class CustomerAutomationTransactionRunner
{
    private readonly IGarageContext _context;
    private readonly ILogger _logger;

    public CustomerAutomationTransactionRunner(IGarageContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                if (tx is not null)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }

                _logger.LogError(ex, "Customer automation transactional operation failed.");
                throw;
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        if (_context.Database.IsRelational())
        {
            return await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        }

        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }
}
