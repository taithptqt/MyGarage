// Mục đích: Xử lý nghiệp vụ chiến dịch voucher như validate dữ liệu, vòng đời chiến dịch và gán voucher vào chiến dịch.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VoucherCampaignService : GenericService<VoucherCampaign>, IVoucherCampaignService
    {
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "DRAFT",
            "PENDING_APPROVAL",
            "SCHEDULED",
            "ACTIVE",
            "PAUSED",
            "ENDED",
            "ARCHIVED"
        };

        private readonly IVoucherCampaignRepository _voucherCampaignRepository;
        private readonly GarageContext _context;

        public VoucherCampaignService(IVoucherCampaignRepository repository, GarageContext context)
            : base(repository)
        {
            _voucherCampaignRepository = repository;
            _context = context;
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
            var now = DateTime.UtcNow;
            NormalizeAndValidate(entity, now);
            entity.CreatedAt ??= now;
            entity.UpdatedAt ??= now;

            if (!string.IsNullOrWhiteSpace(entity.CampaignCode) &&
                await _voucherCampaignRepository.CampaignCodeExistsAsync(entity.CampaignCode))
            {
                throw new InvalidOperationException("Mã chiến dịch đã tồn tại.");
            }

            return await base.AddAsync(entity);
        }

        public override async Task<VoucherCampaign?> UpdateAsync(object id, VoucherCampaign entity)
        {
            if (id is not long campaignId)
            {
                return await base.UpdateAsync(id, entity);
            }

            var existing = await _context.VoucherCampaigns.FirstOrDefaultAsync(x => x.Id == campaignId);
            if (existing == null)
            {
                return null;
            }

            var now = DateTime.UtcNow;
            NormalizeAndValidate(entity, now);
            entity.Id = campaignId;
            entity.CreatedAt = existing.CreatedAt;
            entity.CreatedBy = existing.CreatedBy;
            entity.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(entity.CampaignCode) &&
                await _voucherCampaignRepository.CampaignCodeExistsAsync(entity.CampaignCode, campaignId))
            {
                throw new InvalidOperationException("Mã chiến dịch đã tồn tại.");
            }

            return await base.UpdateAsync(campaignId, entity);
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            if (id is not long campaignId)
            {
                return await base.DeleteAsync(id);
            }

            var campaign = await _context.VoucherCampaigns
                .Include(x => x.Vouchers)
                .FirstOrDefaultAsync(x => x.Id == campaignId);

            if (campaign == null)
            {
                return false;
            }

            var now = DateTime.UtcNow;
            foreach (var voucher in campaign.Vouchers)
            {
                voucher.CampaignId = null;
                voucher.UpdatedAt = now;
                voucher.UpdatedBy = campaign.UpdatedBy;
            }

            _context.VoucherCampaigns.Remove(campaign);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<VoucherCampaign?> AssignVouchersAsync(long campaignId, IEnumerable<long> voucherIds, bool replaceExisting, long? updatedBy = null)
        {
            var campaign = await _context.VoucherCampaigns.FirstOrDefaultAsync(x => x.Id == campaignId && x.IsDeleted != true);
            if (campaign == null)
            {
                return null;
            }

            var normalizedIds = voucherIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            var now = DateTime.UtcNow;

            if (replaceExisting)
            {
                var toUnassign = await _context.Vouchers
                    .Where(v => v.CampaignId == campaignId && !normalizedIds.Contains(v.Id))
                    .ToListAsync();

                foreach (var voucher in toUnassign)
                {
                    voucher.CampaignId = null;
                    voucher.UpdatedAt = now;
                    voucher.UpdatedBy = updatedBy;
                }
            }

            if (normalizedIds.Count > 0)
            {
                var vouchers = await _context.Vouchers
                    .Where(v => normalizedIds.Contains(v.Id) && v.IsDeleted != true)
                    .ToListAsync();

                if (vouchers.Count != normalizedIds.Count)
                {
                    throw new InvalidOperationException("Danh sách voucher có mã không tồn tại hoặc đã bị xóa.");
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
            campaign.BudgetUsed = await _context.Vouchers
                .Where(v => v.CampaignId == campaignId && v.IsDeleted != true)
                .SumAsync(v => (decimal?)v.BudgetUsed) ?? 0;
            campaign.Status = ResolveStatus(campaign.Status, campaign.StartDate, campaign.EndDate, campaign.IsActive, campaign.IsDeleted, now);

            await _context.SaveChangesAsync();
            return await _voucherCampaignRepository.GetDetailedByIdAsync(campaignId, includeDeleted: true);
        }

        public async Task<int> SyncLifecycleStatusesAsync(DateTime? now = null)
        {
            var current = now ?? DateTime.UtcNow;
            var campaigns = await _context.VoucherCampaigns
                .Where(x => x.IsDeleted != true && (x.Status == null || x.Status != "ARCHIVED"))
                .ToListAsync();

            if (campaigns.Count == 0)
            {
                return 0;
            }

            var budgetByCampaign = await _context.Vouchers
                .Where(v => v.CampaignId != null && v.IsDeleted != true)
                .GroupBy(v => v.CampaignId!.Value)
                .Select(g => new { CampaignId = g.Key, BudgetUsed = g.Sum(v => (decimal?)v.BudgetUsed) ?? 0 })
                .ToDictionaryAsync(x => x.CampaignId, x => x.BudgetUsed);

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
                await _context.SaveChangesAsync();
            }

            return changed;
        }

        private static void NormalizeAndValidate(VoucherCampaign entity, DateTime now)
        {
            entity.Name = (entity.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                throw new InvalidOperationException("Tên chiến dịch không được để trống.");
            }

            entity.CampaignCode = string.IsNullOrWhiteSpace(entity.CampaignCode)
                ? null
                : entity.CampaignCode.Trim().ToUpperInvariant();
            entity.Objective = NormalizeNullableText(entity.Objective);
            entity.Description = NormalizeNullableText(entity.Description);
            entity.IsActive ??= true;
            entity.IsDeleted ??= false;
            entity.BudgetLimit ??= 0;
            entity.BudgetUsed ??= 0;

            if (entity.EndDate < entity.StartDate)
            {
                throw new InvalidOperationException("Thời gian kết thúc phải lớn hơn hoặc bằng thời gian bắt đầu.");
            }

            if (entity.BudgetLimit < 0 || entity.BudgetUsed < 0)
            {
                throw new InvalidOperationException("Ngân sách không được âm.");
            }

            if (entity.BudgetLimit.HasValue && entity.BudgetUsed.HasValue && entity.BudgetUsed > entity.BudgetLimit)
            {
                throw new InvalidOperationException("Ngân sách đã dùng không được vượt quá ngân sách giới hạn.");
            }

            if (!string.IsNullOrWhiteSpace(entity.Status))
            {
                var normalized = entity.Status.Trim().ToUpperInvariant();
                if (!AllowedStatuses.Contains(normalized))
                {
                    throw new InvalidOperationException("Trạng thái chiến dịch không hợp lệ.");
                }
                entity.Status = normalized;
            }
            else
            {
                entity.Status = ResolveStatus(entity.Status, entity.StartDate, entity.EndDate, entity.IsActive, entity.IsDeleted, now);
            }
        }

        private static string? NormalizeNullableText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

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
                return "ARCHIVED";
            }

            if (string.Equals(currentStatus, "PENDING_APPROVAL", StringComparison.OrdinalIgnoreCase))
            {
                return "PENDING_APPROVAL";
            }

            if (isActive != true)
            {
                return endDate < now ? "ENDED" : "PAUSED";
            }

            if (endDate < now)
            {
                return "ENDED";
            }

            if (startDate > now)
            {
                return "SCHEDULED";
            }

            return "ACTIVE";
        }
    }
}
