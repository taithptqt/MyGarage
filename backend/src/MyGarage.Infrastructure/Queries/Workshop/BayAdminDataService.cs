using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfBayAdminDataService(IGarageContext context) : IBayAdminDataService
{
    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken ct = default)
    {
        if (context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await context.Database.BeginTransactionAsync(ct);
    }

    public async Task SaveChangesInTransactionAsync(CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(ct);
            await context.SaveChangesAsync(ct);
            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }
        });
    }

    public async Task<IReadOnlyList<BayBranchLookupItem>> GetLookupBranchesAsync(CancellationToken ct = default) =>
        await context.Branches
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(b => new BayBranchLookupItem(b.Id, b.Name, b.Status))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<BayVehicleLookupItem>> GetLookupVehiclesAsync(int takeVehicles, CancellationToken ct = default) =>
        await context.Vehicles
            .AsNoTracking()
            .Where(v => v.IsDeleted != true)
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .OrderByDescending(v => v.UpdatedAt ?? v.CreatedAt)
            .Take(takeVehicles)
            .Select(v => new BayVehicleLookupItem(
                v.Id,
                v.LicensePlate,
                v.Model.ModelName,
                v.Model.Brand.Name,
                v.Model.Segment))
            .ToListAsync(ct);

    public Task<bool> BayExistsAsync(long id, CancellationToken ct = default) =>
        context.Bays.AsNoTracking().AnyAsync(b => b.Id == id, ct);

    public Task<bool> BranchExistsAsync(long branchId, CancellationToken ct = default) =>
        context.Branches.AsNoTracking().AnyAsync(b => b.Id == branchId && b.IsDeleted != true, ct);

    public Task<bool> BayCodeExistsAsync(string code, long? excludeBayId = null, CancellationToken ct = default) =>
        context.Bays
            .AsNoTracking()
            .AnyAsync(
                b => (!excludeBayId.HasValue || b.Id != excludeBayId.Value) && b.IsDeleted != true && b.Code == code,
                ct);

    public Task<bool> BaySequenceOrderExistsAsync(long branchId, int sequenceOrder, long? excludeBayId = null, CancellationToken ct = default) =>
        context.Bays
            .AsNoTracking()
            .AnyAsync(
                b => (!excludeBayId.HasValue || b.Id != excludeBayId.Value) &&
                     b.IsDeleted != true &&
                     b.BranchId == branchId &&
                     b.SequenceOrder == sequenceOrder,
                ct);

    public async Task<int> GetNextSequenceOrderAsync(long branchId, long? excludeBayId = null, CancellationToken ct = default)
    {
        var query = context.Bays
            .AsNoTracking()
            .Where(b => b.BranchId == branchId && b.IsDeleted != true);

        if (excludeBayId.HasValue)
        {
            query = query.Where(b => b.Id != excludeBayId.Value);
        }

        var maxOrder = await query
            .Select(b => (int?)b.SequenceOrder)
            .MaxAsync(ct) ?? 0;

        return Math.Max(1, maxOrder + 1);
    }

    public Task<Bay?> GetBayByIdAsync(long id, CancellationToken ct = default) =>
        context.Bays.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Bay>> GetBaysByIdsAsync(IReadOnlyCollection<long> bayIds, CancellationToken ct = default) =>
        await context.Bays
            .Where(b => bayIds.Contains(b.Id))
            .ToListAsync(ct);

    public async Task AddBayAsync(Bay bay, CancellationToken ct = default)
    {
        context.Bays.Add(bay);
        await SaveChangesInTransactionAsync(ct);
    }

    public async Task<IReadOnlyList<Bay>> GetBaysWithBranchAsync(bool includeHidden, CancellationToken ct = default) =>
        await context.Bays
            .AsNoTracking()
            .Include(b => b.Branch)
            .Where(b => includeHidden || b.IsDeleted != true)
            .ToListAsync(ct);

    public async Task<Dictionary<long, BayVehicleDescriptor>> LoadVehicleMapAsync(IEnumerable<long> vehicleIds, CancellationToken ct = default)
    {
        var ids = vehicleIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<long, BayVehicleDescriptor>();
        }

        var vehicles = await context.Vehicles
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Select(v => new BayVehicleDescriptor
            {
                Id = v.Id,
                LicensePlate = v.LicensePlate,
                ModelName = v.Model.ModelName,
                BrandName = v.Model.Brand.Name,
                Segment = v.Model.Segment
            })
            .ToListAsync(ct);

        return vehicles.ToDictionary(x => x.Id);
    }

    public Task<BayVehicleDescriptor?> LoadVehicleDescriptorAsync(long vehicleId, CancellationToken ct = default) =>
        context.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId && v.IsDeleted != true)
            .Include(v => v.Model)
                .ThenInclude(m => m.Brand)
            .Select(v => new BayVehicleDescriptor
            {
                Id = v.Id,
                LicensePlate = v.LicensePlate,
                ModelName = v.Model.ModelName,
                BrandName = v.Model.Brand.Name,
                Segment = v.Model.Segment
            })
            .FirstOrDefaultAsync(ct);

    public Task<long?> FindOccupiedBayIdByVehicleAsync(long vehicleId, long? ignoreBayId = null, CancellationToken ct = default) =>
        context.Bays
            .AsNoTracking()
            .Where(b =>
                b.IsDeleted != true &&
                b.CurrentOccupiedVehicleId == vehicleId &&
                (!ignoreBayId.HasValue || b.Id != ignoreBayId.Value))
            .Select(b => (long?)b.Id)
            .FirstOrDefaultAsync(ct);
}
