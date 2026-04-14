using System;
using System.Collections.Generic;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherCampaignService
    {
        private static void NormalizeAndValidate(VoucherCampaign entity, DateTime now)
        {
            entity.Name = (entity.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                throw new InvalidOperationException("T\u00ean chi\u1ebfn d\u1ecbch kh\u00f4ng \u0111\u01b0\u1ee3c \u0111\u1ec3 tr\u1ed1ng.");
            }

            entity.CampaignCode = string.IsNullOrWhiteSpace(entity.CampaignCode)
                ? null
                : entity.CampaignCode.Trim().ToUpperInvariant();
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
    }
}
