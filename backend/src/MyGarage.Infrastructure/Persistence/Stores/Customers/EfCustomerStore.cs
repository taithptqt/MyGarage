using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Customers;
using MyGarage.Application.Customers.DTOs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Customers;

public sealed class EfCustomerStore : ICustomerStore
{
    private readonly IGarageContext _context;

    public EfCustomerStore(IGarageContext context)
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

    private async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await action(ct);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<(IReadOnlyList<CustomerAdminItemDto> Items, int TotalCount)> GetCustomerAdminPageAsync(
        CustomerAdminPageQuery query,
        long? branchId,
        string? branchTag,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var baseQuery = _context.Customers
            .AsNoTracking()
            .Where(c => c.IsDeleted != true);

        if (branchId.HasValue && branchId.Value > 0 && !string.IsNullOrEmpty(branchTag))
        {
            var bId = branchId.Value;
            var fromOrders = _context.ServiceOrders
                .Where(so => so.OriginBranchId == bId || so.ExecuteBranchId == bId)
                .Select(so => so.CustomerId);

            var fromAppointments = _context.Appointments
                .Where(a => a.BranchId == bId)
                .Select(a => a.CustomerId);

            baseQuery = baseQuery.Where(c =>
                fromOrders.Contains(c.Id) ||
                fromAppointments.Contains(c.Id) ||
                (c.Note != null && c.Note.Contains(branchTag)));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            baseQuery = baseQuery.Where(c =>
                c.FullName.Contains(search) ||
                (c.Phone != null && c.Phone.Contains(search)) ||
                (c.Email != null && c.Email.Contains(search)));
        }

        if (query.TierId.HasValue && query.TierId.Value > 0)
        {
            baseQuery = baseQuery.Where(c => c.TierId == query.TierId.Value);
        }

        if (query.IsActive.HasValue)
        {
            baseQuery = baseQuery.Where(c => c.IsActive == query.IsActive.Value);
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var items = await baseQuery
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerAdminItemDto(
                c.Id,
                c.FullName,
                c.Phone,
                c.Email,
                c.TierId,
                c.Tier != null ? c.Tier.TierName : null,
                c.TotalSpending ?? 0m,
                c.NoShowCount ?? 0,
                c.IsActive,
                c.Type,
                c.CustomerVehicles
                    .Where(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true)
                    .Select(cv => cv.Vehicle.LicensePlate)
                    .FirstOrDefault(),
                c.CustomerVehicles
                    .Where(cv => cv.IsDeleted != true && cv.IsCurrentOwner == true)
                    .Select(cv => cv.Vehicle.Model.ModelName)
                    .FirstOrDefault(),
                c.Note,
                c.CreatedAt
            ))
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Customer?> GetCustomerWithDetailsAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var query = _context.Customers
            .Include(c => c.Tier)
            .Include(c => c.CustomerVehicles.Where(cv => cv.IsDeleted != true))
                .ThenInclude(cv => cv.Vehicle)
                    .ThenInclude(v => v.Model)
                        .ThenInclude(m => m.Brand)
            .AsQueryable();

        if (!track)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted != true, ct);
    }

    public async Task<Customer?> GetCustomerForDeleteAsync(long id, CancellationToken ct = default)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted != true, ct);
    }

    public async Task<bool> PhoneExistsAsync(string normalizedPhone, long? excludeCustomerId = null, CancellationToken ct = default)
    {
        var query = _context.Customers.AsNoTracking().Where(c => c.Phone == normalizedPhone && c.IsDeleted != true);
        if (excludeCustomerId.HasValue)
        {
            query = query.Where(c => c.Id != excludeCustomerId.Value);
        }
        return await query.AnyAsync(ct);
    }

    public async Task<bool> EmailExistsAsync(string normalizedEmail, long? excludeCustomerId = null, CancellationToken ct = default)
    {
        var query = _context.Customers.AsNoTracking().Where(c => c.Email == normalizedEmail && c.IsDeleted != true);
        if (excludeCustomerId.HasValue)
        {
            query = query.Where(c => c.Id != excludeCustomerId.Value);
        }
        return await query.AnyAsync(ct);
    }

    public Task AddCustomerAsync(Customer customer, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            _context.Customers.Add(customer);
            return Task.CompletedTask;
        }, ct);
    }

    public Task SaveCustomerAsync(Customer customer, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public async Task<IReadOnlyList<CustomerVehicleSummaryDto>> GetCustomerVehiclesAsync(long customerId, CancellationToken ct = default)
    {
        return await _context.CustomerVehicles
            .AsNoTracking()
            .Where(cv => cv.CustomerId == customerId && cv.IsDeleted != true)
            .Include(cv => cv.Vehicle)
                .ThenInclude(v => v.Model)
                    .ThenInclude(m => m.Brand)
            .OrderByDescending(cv => cv.IsCurrentOwner)
            .ThenByDescending(cv => cv.CreatedAt)
            .Select(cv => new CustomerVehicleSummaryDto(
                cv.Id,
                cv.VehicleId,
                cv.Vehicle.LicensePlate,
                cv.Vehicle.Model != null ? cv.Vehicle.Model.ModelName : null,
                cv.Vehicle.Model != null && cv.Vehicle.Model.Brand != null ? cv.Vehicle.Model.Brand.Name : null,
                cv.IsCurrentOwner,
                cv.OwnershipStartDate
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Customer>> GetCustomersByBranchIdAsync(long branchId, CancellationToken ct = default)
    {
        var preferredBranchTag = $"[PreferredBranchId:{branchId}]";

        var customerIdsByBranch = _context.ServiceOrders
            .Where(so => so.OriginBranchId == branchId || so.ExecuteBranchId == branchId)
            .Select(so => so.CustomerId)
            .Distinct();

        var customerIdsByAppointment = _context.Appointments
            .Where(a => a.BranchId == branchId)
            .Select(a => a.CustomerId)
            .Distinct();

        var customerIds = await customerIdsByBranch
            .Union(customerIdsByAppointment)
            .ToListAsync(ct);

        return await _context.Customers
            .Where(c =>
                customerIds.Contains(c.Id) ||
                (c.Note ?? string.Empty).Contains(preferredBranchTag))
            .ToListAsync(ct);
    }

    public async Task<(bool Exists, string? Note)> GetActiveCustomerNoteAsync(long customerId, CancellationToken ct = default)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId && c.IsDeleted != true)
            .Select(c => new { c.Id, c.Note })
            .FirstOrDefaultAsync(ct);

        return customer == null ? (false, null) : (true, customer.Note);
    }

    public async Task<bool> HasBranchServiceOrderAsync(long customerId, long branchId, CancellationToken ct = default)
    {
        return await _context.ServiceOrders
            .AsNoTracking()
            .AnyAsync(so => so.CustomerId == customerId && (so.OriginBranchId == branchId || so.ExecuteBranchId == branchId), ct);
    }

    public async Task<bool> HasBranchAppointmentAsync(long customerId, long branchId, CancellationToken ct = default)
    {
        return await _context.Appointments
            .AsNoTracking()
            .AnyAsync(a => a.CustomerId == customerId && a.BranchId == branchId, ct);
    }
}
