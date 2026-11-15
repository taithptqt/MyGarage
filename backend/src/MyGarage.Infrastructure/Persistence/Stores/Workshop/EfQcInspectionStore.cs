using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfQcInspectionStore : IQcInspectionStore
{
    private readonly IGarageContext _context;

    public EfQcInspectionStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<QcInspection?> GetInspectionForUpdateAsync(long id, CancellationToken ct = default)
    {
        return _context.QcInspections.FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<ServiceOrder?> GetServiceOrderForUpdateAsync(long serviceOrderId, CancellationToken ct = default)
    {
        return _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId, ct);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            await action();
            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync(ct);
    }
}
