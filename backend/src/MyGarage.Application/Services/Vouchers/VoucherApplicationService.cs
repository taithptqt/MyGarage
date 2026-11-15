using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Application.Vouchers;
using MyGarage.Application.Vouchers.DTOs;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class VoucherApplicationService : IVoucherApplicationService
{
    private readonly IVoucherStore _store;
    private readonly IVoucherAuthorizationService _authService;

    public VoucherApplicationService(
        IVoucherStore store,
        IVoucherAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
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

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        string? normalizedStatus = null;
        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            !string.Equals(filter.Status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            normalizedStatus = VoucherLifecycleRules.NormalizeStatus(filter.Status);
        }

        var (vouchers, totalCount, availableStatuses) = await _store.GetAdminListPageAsync(
            filter.IncludeDeleted,
            filter.Search,
            normalizedStatus,
            page,
            pageSize,
            ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var items = vouchers.Select(MapToDto).ToList();

        return new VoucherAdminListResultDto(
            items,
            totalCount,
            page,
            pageSize,
            totalPages,
            availableStatuses.ToList()
        );
    }

    public async Task<VoucherDto?> GetByIdAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanViewVoucherAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết voucher này.");
        }

        var voucher = await _store.GetByIdAsync(id, track: false, ct);
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
        if (await _store.CodeExistsAsync(normCode, ct))
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

        await _store.AddVoucherAsync(voucher, ct);
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

        var voucher = await _store.GetByIdAsync(id, track: true, ct);
        if (voucher == null) return null;

        var now = VietnamTime.Now;
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

        voucher.Status = VoucherLifecycleRules.ResolveStatus(
            now,
            voucher.StartDate,
            voucher.EndDate,
            voucher.IsActive ?? true,
            voucher.IsDeleted ?? false,
            voucher.UsageLimit,
            voucher.UsedCount,
            voucher.BudgetLimit,
            voucher.BudgetUsed);

        await _store.SaveVoucherAsync(voucher, ct);
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

        var voucher = await _store.GetByIdAsync(id, track: true, ct);
        if (voucher == null) return null;

        var targetStatus = VoucherLifecycleRules.NormalizeStatus(status);
        if (!VoucherLifecycleRules.CanTransitionStatus(voucher.Status, targetStatus))
        {
            throw new InvalidOperationException($"Không thể chuyển trạng thái voucher từ '{voucher.Status}' sang '{targetStatus}'.");
        }

        voucher.Status = targetStatus;
        voucher.IsActive = targetStatus == VoucherLifecycleRules.StatusActive;
        voucher.UpdatedBy = actor.UserId;
        voucher.UpdatedAt = VietnamTime.Now;

        await _store.SaveVoucherAsync(voucher, ct);
        return MapToDto(voucher);
    }

    public async Task<bool> DeleteVoucherAsync(long id, UserActorContext actor, CancellationToken ct = default)
    {
        if (!await _authService.CanDeleteVoucherAsync(id, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xóa voucher.");
        }

        var voucher = await _store.GetByIdAsync(id, track: true, ct);
        if (voucher == null) return false;

        voucher.IsDeleted = true;
        voucher.Status = VoucherLifecycleRules.StatusArchived;
        voucher.IsActive = false;
        voucher.UpdatedBy = actor.UserId;
        voucher.UpdatedAt = VietnamTime.Now;

        await _store.SaveVoucherAsync(voucher, ct);
        return true;
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

        Voucher? voucher = null;

        if (input.VoucherId.HasValue && input.VoucherId.Value > 0)
        {
            voucher = await _store.GetByIdAsync(input.VoucherId.Value, track: false, ct);
        }
        else if (!string.IsNullOrWhiteSpace(input.VoucherCode))
        {
            var code = input.VoucherCode.Trim().ToUpperInvariant();
            voucher = await _store.GetByCodeAsync(code, ct);
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
            var customerUses = await _store.CountCustomerRedeemedUsesAsync(voucher.Id, input.CustomerId, ct);
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
