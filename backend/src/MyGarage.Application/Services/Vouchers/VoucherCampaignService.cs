// Mục đích: Xử lý nghiệp vụ chiến dịch voucher như validate dữ liệu, vòng đời chiến dịch và gán voucher vào chiến dịch.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Vouchers;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class VoucherCampaignService : GenericService<VoucherCampaign>, IVoucherCampaignService
{
    private static readonly HashSet<string> AllowedStatuses =
        new(Enum.GetNames<VoucherCampaignStatusCode>(), StringComparer.OrdinalIgnoreCase);

    private readonly IVoucherCampaignRepository _voucherCampaignRepository;
    private readonly IVoucherStore _voucherStore;
    private readonly IMapper _mapper;

    public VoucherCampaignService(IVoucherCampaignRepository repository, IVoucherStore voucherStore, IMapper mapper)
        : base(repository)
    {
        _voucherCampaignRepository = repository;
        _voucherStore = voucherStore;
        _mapper = mapper;
    }

    public async Task<IEnumerable<VoucherCampaign>> GetAllAsync(string? keyword = null, string? status = null, bool includeDeleted = false)
    {
        await SyncLifecycleStatusesAsync();
        return await _voucherCampaignRepository.GetAllAsync(keyword, status, includeDeleted);
    }

    public async Task<VoucherCampaign?> GetDetailedByIdAsync(long id, bool includeDeleted = false)
    {
        await SyncLifecycleStatusesAsync();
        return await _voucherCampaignRepository.GetDetailedByIdAsync(id, includeDeleted);
    }

    public override async Task<VoucherCampaign> AddAsync(VoucherCampaign entity)
    {
        var now = VietnamTime.Now;
        NormalizeAndValidate(entity, now);
        if (entity.CreatedAt == default) entity.CreatedAt = now;
        if (entity.UpdatedAt == default) entity.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(entity.CampaignCode) &&
            await _voucherCampaignRepository.CampaignCodeExistsAsync(entity.CampaignCode))
        {
            throw new InvalidOperationException("M\u00e3 chi\u1ebfn d\u1ecbch \u0111\u00e3 t\u1ed3n t\u1ea1i.");
        }

        return await base.AddAsync(entity);
    }

    public override async Task<VoucherCampaign?> UpdateAsync(object id, VoucherCampaign entity)
    {
        if (id is not long campaignId)
        {
            return await base.UpdateAsync(id, entity);
        }

        var existing = await _voucherStore.GetCampaignByIdAsync(campaignId, track: false);
        if (existing == null)
        {
            return null;
        }

        var now = VietnamTime.Now;
        NormalizeAndValidate(entity, now);
        entity.Id = campaignId;
        entity.CreatedAt = existing.CreatedAt;
        entity.CreatedBy = existing.CreatedBy;
        entity.UpdatedAt = now;

        if (!string.IsNullOrWhiteSpace(entity.CampaignCode) &&
            await _voucherCampaignRepository.CampaignCodeExistsAsync(entity.CampaignCode, campaignId))
        {
            throw new InvalidOperationException("M\u00e3 chi\u1ebfn d\u1ecbch \u0111\u00e3 t\u1ed3n t\u1ea1i.");
        }

        return await base.UpdateAsync(campaignId, entity);
    }

    public override async Task<bool> DeleteAsync(object id)
    {
        if (id is not long campaignId)
        {
            return await base.DeleteAsync(id);
        }

        return await _voucherStore.DeleteCampaignUnassigningVouchersAsync(campaignId, VietnamTime.Now);
    }

    public async Task<VoucherCampaign?> AssignVouchersAsync(
        long campaignId,
        IEnumerable<long> voucherIds,
        bool replaceExisting,
        long? updatedBy = null)
    {
        var campaign = await _voucherStore.GetCampaignByIdAsync(campaignId, track: true, excludeDeleted: true);
        if (campaign == null)
        {
            return null;
        }

        var normalizedIds = voucherIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var now = VietnamTime.Now;

        if (replaceExisting)
        {
            var toUnassign = await _voucherStore.GetVouchersToUnassignFromCampaignAsync(campaignId, normalizedIds);
            foreach (var voucher in toUnassign)
            {
                voucher.CampaignId = null;
                voucher.UpdatedAt = now;
                voucher.UpdatedBy = updatedBy;
            }
        }

        if (normalizedIds.Count > 0)
        {
            var vouchers = await _voucherStore.GetNonDeletedVouchersByIdsAsync(normalizedIds);
            if (vouchers.Count != normalizedIds.Count)
            {
                throw new InvalidOperationException("Danh s\u00e1ch voucher c\u00f3 m\u00e3 kh\u00f4ng t\u1ed3n t\u1ea1i ho\u1eb7c \u0111\u00e3 b\u1ecb x\u00f3a.");
            }

            foreach (var voucher in vouchers)
            {
                voucher.CampaignId = campaignId;
                voucher.UpdatedAt = now;
                voucher.UpdatedBy = updatedBy;
            }
        }

        campaign.UpdatedAt = now;
        campaign.UpdatedBy = updatedBy;
        campaign.BudgetUsed = await _voucherStore.GetCampaignVoucherBudgetUsedAsync(campaignId);
        campaign.Status = ResolveStatus(campaign.Status, campaign.StartDate, campaign.EndDate, campaign.IsActive, campaign.IsDeleted, now);

        await _voucherStore.SaveChangesAsync();
        return await _voucherCampaignRepository.GetDetailedByIdAsync(campaignId, includeDeleted: true);
    }

    public async Task<int> SyncLifecycleStatusesAsync(DateTime? now = null)
    {
        var current = now ?? VietnamTime.Now;
        var campaigns = await _voucherStore.GetActiveLifecycleCampaignsAsync();
        if (campaigns.Count == 0)
        {
            return 0;
        }

        var budgetByCampaign = await _voucherStore.GetVoucherBudgetUsedByCampaignAsync();

        var changed = 0;
        foreach (var campaign in campaigns)
        {
            var originalStatus = (campaign.Status ?? string.Empty).Trim().ToUpperInvariant();
            var expectedStatus = ResolveStatus(campaign.Status, campaign.StartDate, campaign.EndDate, campaign.IsActive, campaign.IsDeleted, current);

            var hasStatusChange = !string.Equals(originalStatus, expectedStatus, StringComparison.OrdinalIgnoreCase);
            var recalculatedBudget = budgetByCampaign.TryGetValue(campaign.Id, out var totalBudget) ? totalBudget : 0;
            var hasBudgetChange = campaign.BudgetUsed != recalculatedBudget;

            if (!hasStatusChange && !hasBudgetChange)
            {
                continue;
            }

            campaign.Status = expectedStatus;
            campaign.BudgetUsed = recalculatedBudget;
            campaign.UpdatedAt = current;
            changed++;
        }

        if (changed > 0)
        {
            await _voucherStore.SaveChangesAsync();
        }

        return changed;
    }

    private static void NormalizeAndValidate(VoucherCampaign entity, DateTime now)
    {
        entity.Name = (entity.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(entity.Name))
        {
            throw new InvalidOperationException("T\u00ean chi\u1ebfn d\u1ecbch kh\u00f4ng \u0111\u01b0\u1ee3c \u0111\u1ec3 tr\u1ed1ng.");
        }

        entity.CampaignCode = NormalizeNullableText(entity.CampaignCode)?.ToUpperInvariant();
        entity.Objective = NormalizeNullableText(entity.Objective);
        entity.Description = NormalizeNullableText(entity.Description);
        entity.BudgetLimit ??= 0;

        if (entity.EndDate < entity.StartDate)
        {
            throw new InvalidOperationException("Th\u1eddi gian k\u1ebft th\u00fac ph\u1ea3i l\u1edbn h\u01a1n ho\u1eb7c b\u1eb1ng th\u1eddi gian b\u1eaft \u0111\u1ea7u.");
        }

        if (entity.BudgetLimit < 0 || entity.BudgetUsed < 0)
        {
            throw new InvalidOperationException("Ng\u00e2n s\u00e1ch kh\u00f4ng \u0111\u01b0\u1ee3c \u00e2m.");
        }

        if (entity.BudgetLimit.HasValue && entity.BudgetUsed > entity.BudgetLimit.Value)
        {
            throw new InvalidOperationException("Ng\u00e2n s\u00e1ch \u0111\u00e3 d\u00f9ng kh\u00f4ng \u0111\u01b0\u1ee3c v\u01b0\u1ee3t qu\u00e1 ng\u00e2n s\u00e1ch gi\u1edbi h\u1ea1n.");
        }

        if (!string.IsNullOrWhiteSpace(entity.Status))
        {
            var normalized = entity.Status.Trim().ToUpperInvariant();
            if (!AllowedStatuses.Contains(normalized))
            {
                throw new InvalidOperationException("Tr\u1ea1ng th\u00e1i chi\u1ebfn d\u1ecbch kh\u00f4ng h\u1ee3p l\u1ec7.");
            }
            entity.Status = normalized;
        }
        else
        {
            entity.Status = ResolveStatus(entity.Status, entity.StartDate, entity.EndDate, entity.IsActive, entity.IsDeleted, now);
        }
    }

    private static string? NormalizeNullableText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveStatus(
        string? currentStatus,
        DateTime startDate,
        DateTime endDate,
        bool? isActive,
        bool? isDeleted,
        DateTime now)
    {
        if (isDeleted == true)
        {
            return nameof(VoucherCampaignStatusCode.ARCHIVED);
        }

        if (string.Equals(currentStatus, nameof(VoucherCampaignStatusCode.PENDING_APPROVAL), StringComparison.OrdinalIgnoreCase))
        {
            return nameof(VoucherCampaignStatusCode.PENDING_APPROVAL);
        }

        if (isActive != true)
        {
            return endDate < now
                ? nameof(VoucherCampaignStatusCode.ENDED)
                : nameof(VoucherCampaignStatusCode.PAUSED);
        }

        if (endDate < now)
        {
            return nameof(VoucherCampaignStatusCode.ENDED);
        }

        if (startDate > now)
        {
            return nameof(VoucherCampaignStatusCode.SCHEDULED);
        }

        return nameof(VoucherCampaignStatusCode.ACTIVE);
    }

    public VoucherCampaignResponseDto MapToDto(VoucherCampaign campaign, decimal budgetWarningThresholdPercent = 80m)
    {
        var vouchers = campaign.Vouchers?
            .Where(v => v.IsDeleted != true)
            .OrderBy(v => v.Code)
            .ThenBy(v => v.Id)
            .Select(v => new VoucherCampaignVoucherSummaryDto
            {
                Id = v.Id,
                Code = v.Code,
                VoucherName = v.VoucherName,
                Type = v.Type,
                Status = v.Status,
                IsActive = v.IsActive,
                StartDate = v.StartDate,
                EndDate = v.EndDate
            })
            .ToList() ?? new List<VoucherCampaignVoucherSummaryDto>();

        var budgetLimit = campaign.BudgetLimit ?? 0;
        var budgetUsed = campaign.BudgetUsed;
        var budgetRemaining = budgetLimit > 0 ? Math.Max(0, budgetLimit - budgetUsed) : 0;
        var budgetUsagePercent = budgetLimit > 0
            ? Math.Round((budgetUsed / budgetLimit) * 100, 2)
            : 0;
        var isBudgetExceeded = budgetLimit > 0 && budgetUsed >= budgetLimit;
        var isNearBudgetLimit = budgetLimit > 0
            && budgetUsagePercent >= budgetWarningThresholdPercent
            && budgetUsagePercent < 100;
        var budgetAlertLevel = budgetLimit <= 0
            ? "NO_LIMIT"
            : isBudgetExceeded
                ? "OVER_LIMIT"
                : isNearBudgetLimit
                    ? "NEAR_LIMIT"
                    : "NORMAL";

        return new VoucherCampaignResponseDto
        {
            Id = campaign.Id,
            CampaignCode = campaign.CampaignCode,
            Name = campaign.Name,
            Objective = campaign.Objective,
            Description = campaign.Description,
            StartDate = campaign.StartDate,
            EndDate = campaign.EndDate,
            BudgetLimit = campaign.BudgetLimit,
            BudgetUsed = campaign.BudgetUsed,
            BudgetRemaining = budgetRemaining,
            BudgetUsagePercent = budgetUsagePercent,
            IsNearBudgetLimit = isNearBudgetLimit,
            IsBudgetExceeded = isBudgetExceeded,
            BudgetAlertLevel = budgetAlertLevel,
            Status = campaign.Status,
            OwnerStaffId = campaign.OwnerStaffId,
            IsActive = campaign.IsActive,
            CreatedAt = campaign.CreatedAt,
            CreatedBy = campaign.CreatedBy,
            UpdatedAt = campaign.UpdatedAt,
            UpdatedBy = campaign.UpdatedBy,
            IsDeleted = campaign.IsDeleted,
            DeletedAt = campaign.DeletedAt,
            DeletedBy = campaign.DeletedBy,
            VoucherCount = vouchers.Count,
            VoucherIds = vouchers.Select(v => v.Id).ToList(),
            Vouchers = vouchers
        };
    }

    public List<VoucherCampaignResponseDto> MapToDtos(IEnumerable<VoucherCampaign> campaigns, decimal budgetWarningThresholdPercent = 80m)
    {
        return campaigns.Select(c => MapToDto(c, budgetWarningThresholdPercent)).ToList();
    }

    public async Task<VoucherCampaignResponseDto?> GetDetailedDtoByIdAsync(long id, bool includeDeleted = false)
    {
        var item = await GetDetailedByIdAsync(id, includeDeleted);
        return item == null ? null : MapToDto(item);
    }

    public async Task<VoucherCampaignResponseDto> CreateDtoAsync(VoucherCampaignCreateDto dto)
    {
        var entity = _mapper.Map<VoucherCampaign>(dto);
        var created = await AddAsync(entity);

        if (dto.VoucherIds != null)
        {
            await AssignVouchersAsync(
                created.Id,
                dto.VoucherIds,
                dto.ReplaceVoucherScope ?? true,
                dto.UpdatedBy ?? dto.CreatedBy);
        }

        var detailed = await GetDetailedByIdAsync(created.Id, includeDeleted: true);
        return MapToDto(detailed ?? created);
    }

    public async Task<VoucherCampaignResponseDto?> UpdateDtoAsync(long id, VoucherCampaignUpdateDto dto)
    {
        var existing = await GetDetailedByIdAsync(id, includeDeleted: true);
        if (existing == null)
        {
            return null;
        }

        _mapper.Map(dto, existing);
        var updated = await UpdateAsync(id, existing);
        if (updated == null)
        {
            return null;
        }

        if (dto.VoucherIds != null)
        {
            await AssignVouchersAsync(id, dto.VoucherIds, dto.ReplaceVoucherScope ?? true, dto.UpdatedBy);
        }

        var detailed = await GetDetailedByIdAsync(id, includeDeleted: true);
        return MapToDto(detailed ?? updated);
    }

    public async Task<VoucherCampaignResponseDto?> AssignVouchersDtoAsync(long campaignId, IEnumerable<long> voucherIds, bool replaceExisting, long? updatedBy = null)
    {
        var updated = await AssignVouchersAsync(campaignId, voucherIds, replaceExisting, updatedBy);
        return updated == null ? null : MapToDto(updated);
    }
}
