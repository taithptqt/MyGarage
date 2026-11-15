using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfBayManagementStore : IBayManagementStore
{
    private readonly IGarageContext _context;

    public EfBayManagementStore(IGarageContext context)
    {
        _context = context;
    }

    public async Task<Bay?> GetBayByIdAsync(long bayId)
    {
        return await _context.Bays.FindAsync(bayId);
    }

    public Task<bool> ServiceOrderExistsAsync(long serviceOrderId)
    {
        return _context.ServiceOrders.AsNoTracking().AnyAsync(x => x.Id == serviceOrderId);
    }

    public async Task<Vehicle?> GetVehicleByIdAsync(long vehicleId)
    {
        return await _context.Vehicles.FindAsync(vehicleId);
    }

    public async Task AddBayAllocationAsync(BayAllocation allocation)
    {
        await _context.BayAllocations.AddAsync(allocation);
    }

    public async Task AddBayStatusLogAsync(BayStatusLog statusLog)
    {
        await _context.BayStatusLogs.AddAsync(statusLog);
    }

    public Task<BayAllocation?> GetActiveBayAllocationAsync(long bayId)
    {
        return _context.BayAllocations
            .Where(a => a.BayId == bayId && a.ReleasedAt == null)
            .OrderByDescending(a => a.AllocatedAt)
            .FirstOrDefaultAsync();
    }

    public Task<BayAllocation> GetBayAllocationWithDetailsAsync(long allocationId)
    {
        return _context.BayAllocations.AsNoTracking()
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Include(a => a.AllocatedByStaff)
            .Include(a => a.ReleasedByStaff)
            .FirstAsync(a => a.Id == allocationId);
    }

    public async Task<IReadOnlyList<Bay>> GetActiveBaysByBranchAsync(long branchId)
    {
        return await _context.Bays
            .Where(b => b.BranchId == branchId && b.IsDeleted == false)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<BayAllocation>> GetBayAllocationsForTimelineAsync(long branchId, DateTime startOfDay, DateTime endOfDay)
    {
        return await _context.BayAllocations
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Include(a => a.AllocatedByStaff)
            .Include(a => a.ReleasedByStaff)
            .Where(a => a.Bay.BranchId == branchId &&
                        a.AllocatedAt < endOfDay &&
                        (a.ReleasedAt == null || a.ReleasedAt >= startOfDay))
            .ToListAsync();
    }

    public async Task SaveChangesInTransactionAsync()
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync();
        });
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            await action();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
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
