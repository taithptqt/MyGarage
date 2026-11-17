using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Bays;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Bays;

public sealed class EfBayStore : IBayStore
{
    private readonly IGarageContext _context;

    public EfBayStore(IGarageContext context)
    {
        _context = context;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
    }

    public async Task<IReadOnlyList<Bay>> GetActiveBaysAsync(long? branchId, CancellationToken ct = default)
    {
        var query = _context.Bays.AsNoTracking().Where(b => b.IsDeleted != true);
        if (branchId.HasValue)
        {
            query = query.Where(b => b.BranchId == branchId.Value);
        }

        return await query.OrderBy(b => b.Id).ToListAsync(ct);
    }

    public Task<Dictionary<long, string>> GetVehicleLicensePlatesAsync(
        IReadOnlyCollection<long> vehicleIds,
        CancellationToken ct = default)
    {
        return _context.Vehicles.AsNoTracking()
            .Where(v => vehicleIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => v.LicensePlate, ct);
    }

    public Task<Bay?> GetBayWithVehicleAsync(long id, CancellationToken ct = default)
    {
        return _context.Bays.AsNoTracking()
            .Include(b => b.CurrentOccupiedVehicle)
            .FirstOrDefaultAsync(b => b.Id == id && b.IsDeleted != true, ct);
    }

    public Task<Bay?> GetBayForUpdateAsync(long id, CancellationToken ct = default)
    {
        return _context.Bays.FirstOrDefaultAsync(b => b.Id == id && b.IsDeleted != true, ct);
    }

    public async Task<Bay?> FindBayByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.Bays.FindAsync(new object[] { id }, ct);
    }

    public async Task<ServiceOrder?> FindServiceOrderByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.ServiceOrders.FindAsync(new object[] { id }, ct);
    }

    public async Task<Vehicle?> FindVehicleByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.Vehicles.FindAsync(new object[] { id }, ct);
    }

    public async Task<BayAllocation?> GetActiveAllocationForBayAsync(long bayId, CancellationToken ct = default)
    {
        var allocations = await _context.BayAllocations
            .Where(a => a.BayId == bayId && a.ReleasedAt == null)
            .OrderByDescending(a => a.AllocatedAt)
            .Take(2)
            .ToListAsync(ct);
        if (allocations.Count > 1)
            throw new BadRequestException("Khoang có nhiều xe đang được phân bổ; cần đối soát trước khi giải phóng.");
        return allocations.SingleOrDefault();
    }

    public async Task<BayAllocation?> GetActiveAllocationForBayAndVehicleAsync(long bayId, long vehicleId, CancellationToken ct = default)
    {
        var allocations = await _context.BayAllocations
            .Where(a => a.BayId == bayId && a.VehicleId == vehicleId && a.ReleasedAt == null)
            .OrderByDescending(a => a.AllocatedAt)
            .Take(2)
            .ToListAsync(ct);
        if (allocations.Count > 1)
            throw new BadRequestException("Xe có nhiều phân bổ đang hoạt động tại khoang; cần đối soát trước khi điều chuyển.");
        return allocations.SingleOrDefault();
    }

    public async Task<IReadOnlyList<BayAllocation>> GetTimelineAllocationsAsync(
        long branchId,
        DateTime startOfDay,
        DateTime endOfDay,
        CancellationToken ct = default)
    {
        return await _context.BayAllocations.AsNoTracking()
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Where(a => a.Bay != null && a.Bay.BranchId == branchId &&
                        a.AllocatedAt < endOfDay &&
                        (a.ReleasedAt == null || a.ReleasedAt >= startOfDay))
            .ToListAsync(ct);
    }

    public Task<BayAllocation> GetAllocationDetailsAsync(
        long allocationId,
        bool includeReleasedByStaff,
        CancellationToken ct = default)
    {
        IQueryable<BayAllocation> query = _context.BayAllocations.AsNoTracking()
            .Include(a => a.Bay)
            .Include(a => a.ServiceOrder)
            .Include(a => a.Vehicle)
            .Include(a => a.AllocatedByStaff);

        if (includeReleasedByStaff)
        {
            query = query.Include(a => a.ReleasedByStaff);
        }

        return query.FirstAsync(a => a.Id == allocationId, ct);
    }

    public async Task AddBayAsync(Bay bay, CancellationToken ct = default)
    {
        await _context.Bays.AddAsync(bay, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateBayAndStatusLogAsync(BayStatusLog? statusLog, CancellationToken ct = default)
    {
        if (statusLog != null)
        {
            await _context.BayStatusLogs.AddAsync(statusLog, ct);
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task SoftDeleteBayAsync(Bay bay, CancellationToken ct = default)
    {
        bay.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
    }

    public async Task AllocateBayInTransactionAsync(
        Bay bay,
        BayAllocation allocation,
        BayStatusLog statusLog,
        long vehicleId,
        DateTime now,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayAllocations.AddAsync(allocation, ct);
            await _context.BayStatusLogs.AddAsync(statusLog, ct);

            bay.OperationalStatus = "IN_USE";
            bay.CurrentOccupiedVehicleId = vehicleId;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    public async Task ReleaseBayInTransactionAsync(
        Bay bay,
        BayStatusLog statusLog,
        DateTime now,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayStatusLogs.AddAsync(statusLog, ct);

            bay.OperationalStatus = "ACTIVE";
            bay.CurrentOccupiedVehicleId = null;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    public async Task TransferBayInTransactionAsync(
        Bay sourceBay,
        Bay targetBay,
        BayAllocation targetAllocation,
        BayStatusLog sourceLog,
        BayStatusLog targetLog,
        long vehicleId,
        DateTime now,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayAllocations.AddAsync(targetAllocation, ct);
            await _context.BayStatusLogs.AddAsync(sourceLog, ct);
            await _context.BayStatusLogs.AddAsync(targetLog, ct);

            sourceBay.OperationalStatus = "ACTIVE";
            sourceBay.CurrentOccupiedVehicleId = null;
            sourceBay.LastStatusChangedAt = now;

            targetBay.OperationalStatus = "IN_USE";
            targetBay.CurrentOccupiedVehicleId = vehicleId;
            targetBay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    public async Task UpdateBayStatusInTransactionAsync(
        Bay bay,
        BayStatusLog statusLog,
        string normalizedTarget,
        DateTime now,
        CancellationToken ct = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();

            await _context.BayStatusLogs.AddAsync(statusLog, ct);

            bay.OperationalStatus = normalizedTarget;
            bay.LastStatusChangedAt = now;

            await _context.SaveChangesAsync(ct);

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }
}
