using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Authorization;
using MyGarage.Application.Vouchers;
using MyGarage.Application.Vouchers.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class VoucherApplicationService : IVoucherApplicationService
{
    private readonly IGarageContext _context;
    private readonly IVoucherAuthorizationService _authService;

    public VoucherApplicationService(
        IGarageContext context,
        IVoucherAuthorizationService authService)
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

    public async Task<VoucherAdminListResultDto> GetAdminListPageAsync(
        VoucherAdminListFilterInput filter,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanViewVouchersAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem danh sách voucher quản trị.");
        }

        var baseQuery = _context.Vouchers.AsNoTracking();
        if (!filter.IncludeDeleted)
        {
            baseQuery = baseQuery.Where(v => v.IsDeleted != true);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(v =>
                (v.Code != null && v.Code.ToUpper().Contains(term)) ||
                (v.VoucherName != null && v.VoucherName.ToUpper().Contains(term)) ||
                (v.Type != null && v.Type.ToUpper().Contains(term)) ||
                (v.Description != null && v.Description.ToUpper().Contains(term)));
        }

        var availableStatuses = await baseQuery
            .Select(v => v.Status)
            .Distinct()
            .OrderBy(s => s)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(filter.Status) && !string.Equals(filter.Status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var norm = VoucherLifecycleRules.NormalizeStatus(filter.Status);
            baseQuery = baseQuery.Where(v => v.Status == norm);
        }

        var totalCount = await baseQuery.CountAsync(ct);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var vouchers = await baseQuery
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = vouchers.Select(MapToDto).ToList();

        return new VoucherAdminListResultDto(
            items,
            totalCount,
            page,
            pageSize,
            totalPages,
            availableStatuses
        );
    }

    public async Task<VoucherDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanViewVoucherAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết voucher này.");
        }

        var voucher = await _context.Vouchers
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);

        return voucher == null ? null : MapToDto(voucher);
    }

    public async Task<VoucherDto> CreateVoucherAsync(
        VoucherCreateInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanCreateVoucherAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo voucher mới.");
        }

        var normCode = input.Code.Trim().ToUpperInvariant();
        var codeExists = await _context.Vouchers
            .AsNoTracking()
            .AnyAsync(v => v.Code == normCode && v.IsDeleted != true, ct);

        if (codeExists)
        {
            throw new InvalidOperationException($"Mã voucher '{normCode}' đã tồn tại trong hệ thống.");
        }

        var now = VietnamTime.Now;
        var initialStatus = VoucherLifecycleRules.ResolveStatus(
            now,
            input.StartDate,
            input.EndDate,
            isActive: true,
            isDeleted: false,
            input.UsageLimit,
            usedCount: 0,
            input.BudgetLimit,
            budgetUsed: 0m);

        var voucher = new Voucher
        {
            Code = normCode,
            VoucherName = input.VoucherName.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            Type = input.Type.Trim().ToUpperInvariant(),
            Value = input.Value,
            MaxDiscountAmount = input.MaxDiscountAmount,
            MinOrderValue = input.MinOrderValue,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            UsageLimit = input.UsageLimit,
            UsedCount = 0,
            MaxUsesPerCustomer = input.MaxUsesPerCustomer,
            MaxUsesPerVehicle = input.MaxUsesPerVehicle,
            FirstUseOnly = input.FirstUseOnly,
            BudgetLimit = input.BudgetLimit,
            BudgetUsed = 0m,
            IssuedCount = 0,
            RedeemedCount = 0,
            Status = initialStatus,
            IsActive = initialStatus == VoucherLifecycleRules.StatusActive,
            CreatedBy = actor.UserId,
            CreatedAt = now,
            UpdatedAt = now,
            Timezone = "Asia/Ho_Chi_Minh"
        };

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return MapToDto(voucher);
    }

    public async Task<VoucherDto?> UpdateVoucherAsync(
        long id,
        VoucherUpdateInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanUpdateVoucherAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền cập nhật voucher.");
        }

        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
        if (voucher == null) return null;

        var now = VietnamTime.Now;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                voucher.VoucherName = input.VoucherName.Trim();
                voucher.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
                voucher.Type = input.Type.Trim().ToUpperInvariant();
                voucher.Value = input.Value;
                voucher.MaxDiscountAmount = input.MaxDiscountAmount;
                voucher.MinOrderValue = input.MinOrderValue;
                voucher.StartDate = input.StartDate;
                voucher.EndDate = input.EndDate;
                voucher.UsageLimit = input.UsageLimit;
                voucher.MaxUsesPerCustomer = input.MaxUsesPerCustomer;
                voucher.MaxUsesPerVehicle = input.MaxUsesPerVehicle;
                voucher.FirstUseOnly = input.FirstUseOnly;
                voucher.BudgetLimit = input.BudgetLimit;
                voucher.UpdatedBy = actor.UserId;
                voucher.UpdatedAt = now;

                var resolvedStatus = VoucherLifecycleRules.ResolveStatus(
                    now,
                    voucher.StartDate,
                    voucher.EndDate,
                    voucher.IsActive ?? true,
                    voucher.IsDeleted ?? false,
                    voucher.UsageLimit,
                    voucher.UsedCount,
                    voucher.BudgetLimit,
                    voucher.BudgetUsed);

                voucher.Status = resolvedStatus;
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return MapToDto(voucher);
    }

    public async Task<VoucherDto?> PatchStatusAsync(
        long id,
        string status,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanPatchVoucherStatusAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền thay đổi trạng thái voucher.");
        }

        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
        if (voucher == null) return null;

        var targetStatus = VoucherLifecycleRules.NormalizeStatus(status);
        if (!VoucherLifecycleRules.CanTransitionStatus(voucher.Status, targetStatus))
        {
            throw new InvalidOperationException($"Không thể chuyển trạng thái voucher từ '{voucher.Status}' sang '{targetStatus}'.");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                voucher.Status = targetStatus;
                voucher.IsActive = targetStatus == VoucherLifecycleRules.StatusActive;
                voucher.UpdatedBy = actor.UserId;
                voucher.UpdatedAt = VietnamTime.Now;
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

        return MapToDto(voucher);
    }

    public async Task<bool> DeleteVoucherAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanDeleteVoucherAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xóa voucher.");
        }

        var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Id == id && v.IsDeleted != true, ct);
        if (voucher == null) return false;

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                voucher.IsDeleted = true;
                voucher.Status = VoucherLifecycleRules.StatusArchived;
                voucher.IsActive = false;
                voucher.UpdatedBy = actor.UserId;
                voucher.UpdatedAt = VietnamTime.Now;
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
                return true;
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<VoucherValidationResultDto> ValidateVoucherAsync(
        VoucherValidationInput input,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanValidateVoucherAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền kiểm tra điều kiện voucher.");
        }

        var query = _context.Vouchers.AsNoTracking().Where(v => v.IsDeleted != true);
        Voucher? voucher = null;

        if (input.VoucherId.HasValue && input.VoucherId.Value > 0)
        {
            voucher = await query.FirstOrDefaultAsync(v => v.Id == input.VoucherId.Value, ct);
        }
        else if (!string.IsNullOrWhiteSpace(input.VoucherCode))
        {
            var code = input.VoucherCode.Trim().ToUpperInvariant();
            voucher = await query.FirstOrDefaultAsync(v => v.Code == code, ct);
        }

        if (voucher == null)
        {
            return new VoucherValidationResultDto(false, "VCH_NOT_FOUND", "Voucher không tồn tại.", null, input.VoucherCode, 0m, input.EligibleAmount);
        }

        if (!string.Equals(voucher.Status, VoucherLifecycleRules.StatusActive, StringComparison.OrdinalIgnoreCase) || voucher.IsActive != true)
        {
            return new VoucherValidationResultDto(false, "VCH_NOT_ACTIVE", $"Voucher không ở trạng thái khả dụng ({voucher.Status}).", voucher.Id, voucher.Code, 0m, input.EligibleAmount);
        }

        var now = VietnamTime.Now;
        if (!VoucherLifecycleRules.IsDateRangeValid(now, voucher.StartDate, voucher.EndDate))
        {
            return new VoucherValidationResultDto(false, "VCH_EXPIRED", "Voucher đã hết hạn hoặc chưa đến ngày áp dụng.", voucher.Id, voucher.Code, 0m, input.EligibleAmount);
        }

        if (!VoucherLifecycleRules.IsEligibleSpend(input.EligibleAmount, voucher.MinOrderValue))
        {
            return new VoucherValidationResultDto(false, "VCH_MIN_SPEND_NOT_MET", $"Giá trị đơn hàng chưa đạt mức tối thiểu {voucher.MinOrderValue:N0}đ.", voucher.Id, voucher.Code, 0m, input.EligibleAmount);
        }

        if (!VoucherLifecycleRules.CanRedeemTotalQuota(voucher.UsageLimit, voucher.UsedCount))
        {
            return new VoucherValidationResultDto(false, "VCH_QUOTA_EXHAUSTED", "Voucher đã hết lượt sử dụng trên toàn hệ thống.", voucher.Id, voucher.Code, 0m, input.EligibleAmount);
        }

        if (voucher.MaxUsesPerCustomer > 0 && input.CustomerId > 0)
        {
            var customerUses = await _context.VoucherRedemptions
                .AsNoTracking()
                .CountAsync(r => r.VoucherId == voucher.Id && r.CustomerId == input.CustomerId && r.RedemptionStatus == "REDEEMED", ct);
            if (!VoucherLifecycleRules.CanRedeemCustomerQuota(voucher.MaxUsesPerCustomer, customerUses))
            {
                return new VoucherValidationResultDto(false, "VCH_CUSTOMER_LIMIT_REACHED", $"Bạn đã dùng hết hạn mức của voucher này ({voucher.MaxUsesPerCustomer} lần).", voucher.Id, voucher.Code, 0m, input.EligibleAmount);
            }
        }

        var discount = VoucherLifecycleRules.CalculateDiscount(
            voucher.Type,
            voucher.Value,
            voucher.MaxDiscountAmount,
            input.EligibleAmount);

        if (!VoucherLifecycleRules.CanRedeemBudget(voucher.BudgetLimit, voucher.BudgetUsed, discount))
        {
            return new VoucherValidationResultDto(false, "VCH_BUDGET_EXHAUSTED", "Ngân sách phát hành của voucher này đã hết.", voucher.Id, voucher.Code, 0m, input.EligibleAmount);
        }

        return new VoucherValidationResultDto(true, null, "Voucher hợp lệ và sẵn sàng áp dụng.", voucher.Id, voucher.Code, discount, input.EligibleAmount);
    }

    private static VoucherDto MapToDto(Voucher v)
    {
        return new VoucherDto(
            v.Id,
            v.Code,
            v.VoucherName,
            v.Description,
            v.Type,
            v.Value,
            v.MaxDiscountAmount,
            v.MinOrderValue,
            v.StartDate,
            v.EndDate,
            v.UsageLimit,
            v.UsedCount,
            v.MaxUsesPerCustomer,
            v.MaxUsesPerVehicle,
            v.FirstUseOnly,
            v.BudgetLimit,
            v.BudgetUsed,
            v.Status,
            v.IsActive,
            v.IsDeleted,
            v.CreatedAt
        );
    }
}
