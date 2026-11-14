using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Customers;
using MyGarage.Application.Customers.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class CustomerApplicationService : ICustomerApplicationService
{
    private readonly IGarageContext _context;
    private readonly ICustomerAuthorizationService _authService;

    public CustomerApplicationService(
        IGarageContext context,
        ICustomerAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
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

    public async Task<CustomerAdminPageResult> GetCustomerAdminPageAsync(
        CustomerAdminPageQuery query,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageCustomersAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập danh sách khách hàng quản trị.");
        }

        var baseQuery = _context.Customers
            .AsNoTracking()
            .Where(c => c.IsDeleted != true);

        // Branch scoping
        var branchId = query.BranchId ?? actor.BranchId;
        if (branchId.HasValue && branchId.Value > 0)
        {
            var bId = branchId.Value;
            var fromOrders = _context.ServiceOrders
                .Where(so => so.OriginBranchId == bId || so.ExecuteBranchId == bId)
                .Select(so => so.CustomerId);

            var fromAppointments = _context.Appointments
                .Where(a => a.BranchId == bId)
                .Select(a => a.CustomerId);

            var branchTag = CustomerDomainRules.FormatPreferredBranchTag(bId);

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

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

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

        return new CustomerAdminPageResult(items, totalCount, page, pageSize);
    }

    public async Task<CustomerDetailDto?> GetCustomerByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanAccessCustomerAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException($"Không có quyền truy cập thông tin khách hàng #{id}.");
        }

        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.Tier)
            .Include(c => c.CustomerVehicles.Where(cv => cv.IsDeleted != true))
                .ThenInclude(cv => cv.Vehicle)
                    .ThenInclude(v => v.Model)
                        .ThenInclude(m => m.Brand)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted != true, ct);

        if (customer == null) return null;

        var vehicleDtos = customer.CustomerVehicles
            .Select(cv => new CustomerVehicleSummaryDto(
                cv.Id,
                cv.VehicleId,
                cv.Vehicle.LicensePlate,
                cv.Vehicle.Model?.ModelName,
                cv.Vehicle.Model?.Brand?.Name,
                cv.IsCurrentOwner,
                cv.OwnershipStartDate
            ))
            .ToList();

        return new CustomerDetailDto(
            customer.Id,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.TierId,
            customer.Tier?.TierName,
            customer.TotalSpending ?? 0m,
            customer.NoShowCount ?? 0,
            customer.IsActive,
            customer.Type,
            customer.Note,
            customer.CreatedAt,
            vehicleDtos
        );
    }

    public async Task<CustomerDetailDto> CreateCustomerAsync(
        CustomerCreateCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageCustomersAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo khách hàng.");
        }

        if (string.IsNullOrWhiteSpace(command.FullName))
        {
            throw new ArgumentException("Họ và tên khách hàng không được để trống.", nameof(command.FullName));
        }

        var normPhone = CustomerDomainRules.NormalizePhone(command.Phone);
        if (!string.IsNullOrEmpty(normPhone) && !CustomerDomainRules.IsValidVietnamPhone(normPhone))
        {
            throw new ArgumentException("Số điện thoại không đúng định dạng Việt Nam.", nameof(command.Phone));
        }

        var normEmail = CustomerDomainRules.NormalizeEmail(command.Email);
        if (!string.IsNullOrEmpty(normEmail) && !CustomerDomainRules.IsValidEmail(normEmail))
        {
            throw new ArgumentException("Email không đúng định dạng.", nameof(command.Email));
        }

        // Duplicate checks
        if (!string.IsNullOrEmpty(normPhone))
        {
            var phoneExists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Phone == normPhone && c.IsDeleted != true, ct);
            if (phoneExists)
            {
                throw new InvalidOperationException($"Số điện thoại '{normPhone}' đã tồn tại trong hệ thống.");
            }
        }

        if (!string.IsNullOrEmpty(normEmail))
        {
            var emailExists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Email == normEmail && c.IsDeleted != true, ct);
            if (emailExists)
            {
                throw new InvalidOperationException($"Email '{normEmail}' đã tồn tại trong hệ thống.");
            }
        }

        var type = command.Type;
        if (string.IsNullOrWhiteSpace(type) || !CustomerDomainRules.IsValidCustomerType(type))
        {
            type = CustomerDomainRules.TypeGuest;
        }

        var note = command.Note?.Trim();
        if (command.PreferredBranchId.HasValue && command.PreferredBranchId.Value > 0)
        {
            note = CustomerDomainRules.SetPreferredBranchTag(note, command.PreferredBranchId.Value);
        }

        var now = VietnamTime.Now;
        var customer = new Customer
        {
            FullName = command.FullName.Trim(),
            Phone = normPhone,
            Email = normEmail,
            Type = type.Trim().ToUpperInvariant(),
            TierId = command.TierId,
            Note = note,
            IsActive = true,
            TotalSpending = 0m,
            NoShowCount = 0,
            CreatedAt = now,
            CreatedBy = actor.UserId,
            UpdatedAt = now,
            UpdatedBy = actor.UserId,
            IsDeleted = false
        };

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return new CustomerDetailDto(
            customer.Id,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.TierId,
            null,
            0m,
            0,
            customer.IsActive,
            customer.Type,
            customer.Note,
            customer.CreatedAt,
            Array.Empty<CustomerVehicleSummaryDto>()
        );
    }

    public async Task<CustomerDetailDto?> UpdateCustomerAsync(
        long id,
        CustomerUpdateCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return null;

        if (!await _authService.CanAccessCustomerAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException($"Không có quyền cập nhật khách hàng #{id}.");
        }

        var customer = await _context.Customers
            .Include(c => c.Tier)
            .Include(c => c.CustomerVehicles.Where(cv => cv.IsDeleted != true))
                .ThenInclude(cv => cv.Vehicle)
                    .ThenInclude(v => v.Model)
                        .ThenInclude(m => m.Brand)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted != true, ct);

        if (customer == null) return null;

        if (string.IsNullOrWhiteSpace(command.FullName))
        {
            throw new ArgumentException("Họ và tên khách hàng không được để trống.", nameof(command.FullName));
        }

        var normPhone = CustomerDomainRules.NormalizePhone(command.Phone);
        if (!string.IsNullOrEmpty(normPhone) && !CustomerDomainRules.IsValidVietnamPhone(normPhone))
        {
            throw new ArgumentException("Số điện thoại không đúng định dạng Việt Nam.", nameof(command.Phone));
        }

        var normEmail = CustomerDomainRules.NormalizeEmail(command.Email);
        if (!string.IsNullOrEmpty(normEmail) && !CustomerDomainRules.IsValidEmail(normEmail))
        {
            throw new ArgumentException("Email không đúng định dạng.", nameof(command.Email));
        }

        // Duplicate checks
        if (!string.IsNullOrEmpty(normPhone))
        {
            var phoneExists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Phone == normPhone && c.Id != id && c.IsDeleted != true, ct);
            if (phoneExists)
            {
                throw new InvalidOperationException($"Số điện thoại '{normPhone}' đã được sử dụng bởi khách hàng khác.");
            }
        }

        if (!string.IsNullOrEmpty(normEmail))
        {
            var emailExists = await _context.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Email == normEmail && c.Id != id && c.IsDeleted != true, ct);
            if (emailExists)
            {
                throw new InvalidOperationException($"Email '{normEmail}' đã được sử dụng bởi khách hàng khác.");
            }
        }

        customer.FullName = command.FullName.Trim();
        customer.Phone = normPhone;
        customer.Email = normEmail;

        if (!string.IsNullOrWhiteSpace(command.Type) && CustomerDomainRules.IsValidCustomerType(command.Type))
        {
            customer.Type = command.Type.Trim().ToUpperInvariant();
        }

        if (command.TierId.HasValue)
        {
            customer.TierId = command.TierId.Value;
        }

        if (command.IsActive.HasValue)
        {
            customer.IsActive = command.IsActive.Value;
        }

        var note = command.Note?.Trim() ?? customer.Note;
        if (command.PreferredBranchId.HasValue && command.PreferredBranchId.Value > 0)
        {
            note = CustomerDomainRules.SetPreferredBranchTag(note, command.PreferredBranchId.Value);
        }
        customer.Note = note;

        var now = VietnamTime.Now;
        customer.UpdatedAt = now;
        customer.UpdatedBy = actor.UserId;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        var vehicleDtos = customer.CustomerVehicles
            .Select(cv => new CustomerVehicleSummaryDto(
                cv.Id,
                cv.VehicleId,
                cv.Vehicle.LicensePlate,
                cv.Vehicle.Model?.ModelName,
                cv.Vehicle.Model?.Brand?.Name,
                cv.IsCurrentOwner,
                cv.OwnershipStartDate
            ))
            .ToList();

        return new CustomerDetailDto(
            customer.Id,
            customer.FullName,
            customer.Phone,
            customer.Email,
            customer.TierId,
            customer.Tier?.TierName,
            customer.TotalSpending ?? 0m,
            customer.NoShowCount ?? 0,
            customer.IsActive,
            customer.Type,
            customer.Note,
            customer.CreatedAt,
            vehicleDtos
        );
    }

    public async Task<bool> DeleteCustomerAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (id <= 0) return false;

        if (!await _authService.CanManageCustomersAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xóa khách hàng.");
        }

        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted != true, ct);

        if (customer == null) return false;

        var now = VietnamTime.Now;
        customer.IsDeleted = true;
        customer.DeletedAt = now;
        customer.DeletedBy = actor.UserId;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return true;
    }

    public async Task<IReadOnlyList<CustomerVehicleSummaryDto>> GetCustomerVehiclesAsync(
        long customerId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (customerId <= 0) return Array.Empty<CustomerVehicleSummaryDto>();

        if (!await _authService.CanAccessCustomerAsync(customerId, actor, ct))
        {
            throw new UnauthorizedAccessException($"Không có quyền xem phương tiện của khách hàng #{customerId}.");
        }

        var vehicles = await _context.CustomerVehicles
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

        return vehicles;
    }
}
