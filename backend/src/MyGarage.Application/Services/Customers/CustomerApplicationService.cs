using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Customers;
using MyGarage.Application.Customers.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class CustomerApplicationService : ICustomerApplicationService
{
    private readonly ICustomerStore _store;
    private readonly ICustomerAuthorizationService _authService;

    public CustomerApplicationService(
        ICustomerStore store,
        ICustomerAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
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

        var branchId = query.BranchId ?? actor.BranchId;
        var branchTag = branchId.HasValue && branchId.Value > 0
            ? CustomerDomainRules.FormatPreferredBranchTag(branchId.Value)
            : null;

        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;

        var (items, totalCount) = await _store.GetCustomerAdminPageAsync(query, branchId, branchTag, page, pageSize, ct);
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

        var customer = await _store.GetCustomerWithDetailsAsync(id, track: false, ct);
        if (customer == null) return null;

        return MapDetailDto(customer);
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

        if (!string.IsNullOrEmpty(normPhone) && await _store.PhoneExistsAsync(normPhone, null, ct))
        {
            throw new InvalidOperationException($"Số điện thoại '{normPhone}' đã tồn tại trong hệ thống.");
        }

        if (!string.IsNullOrEmpty(normEmail) && await _store.EmailExistsAsync(normEmail, null, ct))
        {
            throw new InvalidOperationException($"Email '{normEmail}' đã tồn tại trong hệ thống.");
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

        await _store.AddCustomerAsync(customer, ct);

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

        var customer = await _store.GetCustomerWithDetailsAsync(id, track: true, ct);
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

        if (!string.IsNullOrEmpty(normPhone) && await _store.PhoneExistsAsync(normPhone, id, ct))
        {
            throw new InvalidOperationException($"Số điện thoại '{normPhone}' đã được sử dụng bởi khách hàng khác.");
        }

        if (!string.IsNullOrEmpty(normEmail) && await _store.EmailExistsAsync(normEmail, id, ct))
        {
            throw new InvalidOperationException($"Email '{normEmail}' đã được sử dụng bởi khách hàng khác.");
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

        customer.UpdatedAt = VietnamTime.Now;
        customer.UpdatedBy = actor.UserId;

        await _store.SaveCustomerAsync(customer, ct);

        return MapDetailDto(customer);
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

        var customer = await _store.GetCustomerForDeleteAsync(id, ct);
        if (customer == null) return false;

        customer.IsDeleted = true;
        customer.DeletedAt = VietnamTime.Now;
        customer.DeletedBy = actor.UserId;

        await _store.SaveCustomerAsync(customer, ct);

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

        return await _store.GetCustomerVehiclesAsync(customerId, ct);
    }

    private static CustomerDetailDto MapDetailDto(Customer customer)
    {
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
}
