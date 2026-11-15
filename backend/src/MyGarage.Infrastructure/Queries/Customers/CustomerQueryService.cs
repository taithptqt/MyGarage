using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfCustomerQueryService(IGarageContext context) : ICustomerQueryService
{
    public async Task<(IReadOnlyList<Customer> Items, int Total)> GetScopedPageAsync(
        long? branchId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = ScopedCustomers(branchId).OrderBy(c => c.Id);
        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var items = offset >= total
            ? []
            : await query.Skip((int)offset).Take(pageSize).ToListAsync(ct);

        return (items, total);
    }

    public Task<bool> ExistsInScopeAsync(long id, long? branchId, CancellationToken ct = default)
        => ScopedCustomers(branchId).AnyAsync(x => x.Id == id, ct);

    public async Task<CustomerAdminPageResult> GetAdminPageAsync(
        long? branchId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var customers = ScopedCustomers(branchId);
        var totalCustomers = await customers.CountAsync(ct);
        var activeCustomers = await customers.CountAsync(x => x.IsActive == true, ct);

        // Legacy view chooses the last current-owner relation, or the first relation if none is current.
        var query = customers.Select(c => new
        {
            Customer = c,
            TierName = c.Tier != null ? c.Tier.TierName : null,
            Primary = c.CustomerVehicles.Where(link => link.IsDeleted != true)
                .OrderByDescending(link => link.IsCurrentOwner == true)
                .ThenBy(link => link.IsCurrentOwner == true ? -link.Id : link.Id)
                .Select(link => new
                {
                    LinkId = link.Id,
                    link.VehicleId,
                    Exists = link.Vehicle.IsDeleted != true,
                    Plate = link.Vehicle.LicensePlate,
                    link.Vehicle.Color,
                    link.Vehicle.ManufactureYear,
                    link.Vehicle.ModelId,
                    link.Vehicle.Model.ModelName,
                    link.Vehicle.Model.BrandId,
                    BrandName = link.Vehicle.Model.Brand.Name
                })
                .FirstOrDefault()
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => (x.Customer.FullName ?? "").ToLower().Contains(term) ||
                (x.Customer.Email ?? "").ToLower().Contains(term) || (x.Customer.Phone ?? "").ToLower().Contains(term) ||
                (x.TierName ?? "-").ToLower().Contains(term) ||
                ((x.Primary != null && x.Primary.Exists ? x.Primary.Plate : "-") ?? "-").ToLower().Contains(term) ||
                ((x.Primary != null && x.Primary.Exists ? x.Primary.BrandName : "-") ?? "-").ToLower().Contains(term) ||
                ((x.Primary != null && x.Primary.Exists ? x.Primary.ModelName : "-") ?? "-").ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        var pageRows = await query.OrderBy(x => x.Customer.Id)
            .Skip(offset >= total ? total : (int)offset).Take(pageSize)
            .ToListAsync(ct);

        var rows = pageRows.Select(x => new CustomerAdminRow(
            x.Customer,
            x.TierName,
            x.Primary == null
                ? null
                : new CustomerAdminPrimaryVehicleRow(
                    x.Primary.LinkId,
                    x.Primary.VehicleId,
                    x.Primary.Exists,
                    x.Primary.Plate,
                    x.Primary.Color,
                    x.Primary.ManufactureYear,
                    x.Primary.ModelId,
                    x.Primary.ModelName,
                    x.Primary.BrandId,
                    x.Primary.BrandName)))
            .ToList();

        return new CustomerAdminPageResult(rows, total, totalCustomers, activeCustomers);
    }

    private IQueryable<Customer> ScopedCustomers(long? branchId)
    {
        var query = context.Customers.AsNoTracking().AsQueryable();
        if (!branchId.HasValue) return query;

        var id = branchId.Value;
        var tag = $"[PreferredBranchId:{id}]";
        var fromOrders = context.ServiceOrders.Where(x => x.OriginBranchId == id || x.ExecuteBranchId == id).Select(x => x.CustomerId);
        var fromAppointments = context.Appointments.Where(x => x.BranchId == id).Select(x => x.CustomerId);
        return query.Where(x => fromOrders.Contains(x.Id) || fromAppointments.Contains(x.Id) || (x.Note ?? "").Contains(tag));
    }
}
