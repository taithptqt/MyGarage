using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfVehicleAdminDataService(IGarageContext context) : IVehicleAdminDataService
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

    public async Task<IReadOnlyList<VehicleAdminBranchLookup>> GetLookupBranchesAsync(CancellationToken ct = default) =>
        await context.Branches
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(b => new VehicleAdminBranchLookup(b.Id, b.Name, b.Status))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VehicleAdminBrandLookup>> GetLookupBrandsAsync(CancellationToken ct = default) =>
        await context.VehicleBrands
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .OrderBy(b => b.Name)
            .Select(b => new VehicleAdminBrandLookup(b.Id, b.Code, b.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VehicleAdminModelLookup>> GetLookupModelsAsync(CancellationToken ct = default) =>
        await context.VehicleModels
            .AsNoTracking()
            .Where(m => m.IsDeleted != true)
            .Include(m => m.Brand)
            .OrderBy(m => m.ModelName)
            .Select(m => new VehicleAdminModelLookup(m.Id, m.BrandId, m.Brand.Name, m.ModelName, m.Segment))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VehicleAdminBayLookup>> GetLookupBaysAsync(CancellationToken ct = default) =>
        await context.Bays
            .AsNoTracking()
            .Where(b => b.IsDeleted != true)
            .Include(b => b.Branch)
            .OrderBy(b => b.Code)
            .Select(b => new VehicleAdminBayLookup(
                b.Id,
                b.Code,
                b.Name,
                b.BranchId,
                b.Branch.Name,
                b.OperationalStatus,
                b.IsActive == false))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VehicleAdminStaffLookup>> GetLookupStaffsAsync(CancellationToken ct = default) =>
        await context.Staffs
            .AsNoTracking()
            .Where(s => s.IsDeleted != true)
            .Include(s => s.Role)
            .Include(s => s.Branch)
            .OrderBy(s => s.FullName)
            .Select(s => new VehicleAdminStaffLookup(
                s.Id,
                s.FullName,
                s.BranchId,
                s.Branch != null ? s.Branch.Name : null,
                s.Role != null ? s.Role.RoleName : null,
                s.Status))
            .ToListAsync(ct);

    public Task<bool> VehicleModelExistsAsync(long modelId, CancellationToken ct = default) =>
        context.VehicleModels.AsNoTracking().AnyAsync(m => m.Id == modelId && m.IsDeleted != true, ct);

    public Task<bool> VehiclePlateExistsAsync(string normalizedPlate, long? excludeVehicleId = null, CancellationToken ct = default) =>
        context.Vehicles.AsNoTracking().AnyAsync(
            v => (!excludeVehicleId.HasValue || v.Id != excludeVehicleId.Value) &&
                 v.IsDeleted != true &&
                 v.LicensePlate == normalizedPlate,
            ct);

    public Task<bool> VehicleVinExistsAsync(string normalizedVin, long? excludeVehicleId = null, CancellationToken ct = default) =>
        context.Vehicles.AsNoTracking().AnyAsync(
            v => (!excludeVehicleId.HasValue || v.Id != excludeVehicleId.Value) &&
                 v.IsDeleted != true &&
                 v.Vin == normalizedVin,
            ct);

    public Task<bool> CustomerExistsAsync(long customerId, CancellationToken ct = default) =>
        context.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId && c.IsDeleted != true, ct);

    public Task<bool> VehicleExistsAsync(long id, bool activeOnly = false, CancellationToken ct = default) =>
        context.Vehicles.AsNoTracking().AnyAsync(v => v.Id == id && (!activeOnly || v.IsDeleted != true), ct);

    public async Task AddVehicleAsync(Vehicle vehicle, CancellationToken ct = default)
    {
        context.Vehicles.Add(vehicle);
        await SaveChangesInTransactionAsync(ct);
    }

    public Task<Vehicle?> GetVehicleByIdAsync(long id, CancellationToken ct = default) =>
        context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<Vehicle?> GetActiveVehicleWithModelAsync(long id, CancellationToken ct = default) =>
        context.Vehicles.AsNoTracking()
            .Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);

    public Task<Staff?> GetActiveStaffByIdAsync(long staffId, CancellationToken ct = default) =>
        context.Staffs.AsNoTracking().FirstOrDefaultAsync(s => s.Id == staffId && s.IsDeleted != true, ct);

    public Task<string?> GetStaffFullNameAsync(long staffId, CancellationToken ct = default) =>
        context.Staffs.AsNoTracking()
            .Where(s => s.Id == staffId)
            .Select(s => s.FullName)
            .FirstOrDefaultAsync(ct);

    public Task<Bay?> GetActiveBayByIdAsync(long bayId, CancellationToken ct = default) =>
        context.Bays.FirstOrDefaultAsync(b => b.Id == bayId && b.IsDeleted != true, ct);

    public Task<Bay?> GetBayByIdAsync(long bayId, CancellationToken ct = default) =>
        context.Bays.FirstOrDefaultAsync(b => b.Id == bayId, ct);

    public async Task<IReadOnlyList<Bay>> GetActiveBaysOccupiedByVehicleAsync(long vehicleId, CancellationToken ct = default) =>
        await context.Bays.Where(b => b.CurrentOccupiedVehicleId == vehicleId && b.IsDeleted != true).ToListAsync(ct);

    public Task<Bay?> GetFirstActiveBayOccupiedByVehicleAsync(long vehicleId, CancellationToken ct = default) =>
        context.Bays.FirstOrDefaultAsync(b => b.CurrentOccupiedVehicleId == vehicleId && b.IsDeleted != true, ct);

    public Task<long?> FindOccupiedBayIdByVehicleAsync(long vehicleId, CancellationToken ct = default) =>
        context.Bays.AsNoTracking()
            .Where(b => b.CurrentOccupiedVehicleId == vehicleId && b.IsDeleted != true)
            .Select(b => (long?)b.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<VehicleAdminRowSourceData> GetVehicleRowSourceDataAsync(
        bool includeHidden,
        IReadOnlyCollection<long> activeSessionBayIds,
        CancellationToken ct = default)
    {
        var query = context.Vehicles.AsNoTracking()
            .Include(v => v.Model)
            .ThenInclude(m => m.Brand)
            .AsQueryable();

        if (!includeHidden)
        {
            query = query.Where(v => v.IsDeleted != true);
        }

        var vehicles = await query.ToListAsync(ct);
        if (vehicles.Count == 0)
        {
            return new VehicleAdminRowSourceData(
                Array.Empty<Vehicle>(),
                Array.Empty<CustomerVehicle>(),
                Array.Empty<VehicleAdminServiceOrderLite>(),
                Array.Empty<WorkSlot>(),
                Array.Empty<Bay>(),
                Array.Empty<Bay>());
        }

        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        var ownerLinks = await context.CustomerVehicles.AsNoTracking()
            .Where(cv => vehicleIds.Contains(cv.VehicleId) && cv.IsDeleted != true)
            .Include(cv => cv.Customer)
            .ToListAsync(ct);

        var orders = await context.ServiceOrders.AsNoTracking()
            .Where(o => vehicleIds.Contains(o.VehicleId))
            .Select(o => new VehicleAdminServiceOrderLite
            {
                VehicleId = o.VehicleId,
                ExecuteBranchId = o.ExecuteBranchId,
                Status = o.Status,
                CheckInTime = o.CheckInTime,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(ct);

        var workSlots = await context.WorkSlots.AsNoTracking()
            .Where(w => w.VehicleId.HasValue && vehicleIds.Contains(w.VehicleId.Value))
            .Include(w => w.Technician)
            .ToListAsync(ct);

        var fallbackOccupiedBays = await context.Bays.AsNoTracking()
            .Where(b => b.CurrentOccupiedVehicleId.HasValue && vehicleIds.Contains(b.CurrentOccupiedVehicleId.Value) && b.IsDeleted != true)
            .Include(b => b.Branch)
            .ToListAsync(ct);

        var bayIds = activeSessionBayIds
            .Concat(fallbackOccupiedBays.Select(x => x.Id))
            .Distinct()
            .ToList();

        var baysByIds = bayIds.Count == 0
            ? new List<Bay>()
            : await context.Bays.AsNoTracking()
                .Where(b => bayIds.Contains(b.Id))
                .Include(b => b.Branch)
                .ToListAsync(ct);

        return new VehicleAdminRowSourceData(
            vehicles,
            ownerLinks,
            orders,
            workSlots,
            fallbackOccupiedBays,
            baysByIds);
    }

    public async Task<Dictionary<long, Staff>> GetStaffMapAsync(IReadOnlyCollection<long> staffIds, CancellationToken ct = default) =>
        staffIds.Count == 0
            ? new Dictionary<long, Staff>()
            : await context.Staffs.AsNoTracking()
                .Where(s => staffIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s, ct);

    public async Task<Dictionary<long, Branch>> GetBranchMapAsync(IReadOnlyCollection<long> branchIds, CancellationToken ct = default) =>
        branchIds.Count == 0
            ? new Dictionary<long, Branch>()
            : await context.Branches.AsNoTracking()
                .Where(b => branchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b, ct);

    public async Task<IReadOnlyList<CustomerVehicle>> GetActiveCustomerVehicleLinksAsync(long vehicleId, CancellationToken ct = default) =>
        await context.CustomerVehicles
            .Where(cv => cv.VehicleId == vehicleId && cv.IsDeleted != true)
            .ToListAsync(ct);

    public void AddCustomerVehicle(CustomerVehicle entity) =>
        context.CustomerVehicles.Add(entity);
}
