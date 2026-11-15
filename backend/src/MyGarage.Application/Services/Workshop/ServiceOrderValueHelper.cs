using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static class ServiceOrderValueHelper
    {
        internal static string? NormalizeText(string? value)
        {
            var text = value?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        internal static string? NormalizeToken(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }

        internal static List<long> NormalizePositiveLongList(IEnumerable<long>? values)
        {
            return values?
                .Select(value => value > 0 ? value : 0)
                .Where(value => value > 0)
                .Distinct()
                .ToList() ?? new List<long>();
        }

        internal static string ResolveOrderStatus(string? status)
        {
            var token = NormalizeToken(status);
            if (string.IsNullOrWhiteSpace(token))
            {
                return nameof(ServiceOrderLifecycleStatus.NEW);
            }

            if (!Enum.TryParse<ServiceOrderLifecycleStatus>(token, ignoreCase: true, out _))
            {
                throw new InvalidOperationException($"Trạng thái lệnh sửa chữa không hợp lệ: {status}.");
            }

            return token;
        }

        internal static decimal ResolveServiceOrderTotalDiscountAmount(ServiceOrder? order)
        {
            if (order == null)
            {
                return 0m;
            }

            return (order.ServiceOrderDiscounts ?? new List<ServiceOrderDiscount>())
                .Where(x => x != null)
                .Sum(x => x.AppliedAmount > 0m ? x.AppliedAmount : Math.Max(0m, x.DiscountValue));
        }

        internal static void ApplyInsuranceSnapshot(
            ServiceOrder order,
            bool insuranceEnabled,
            long? insuranceCompanyId,
            decimal? deductibleAmount,
            string? deductibleOption,
            string? claimMode,
            string? quoteState)
        {
            if (!insuranceEnabled || !insuranceCompanyId.HasValue || insuranceCompanyId.Value <= 0)
            {
                order.InsuranceDeductibleAmount = null;
                order.InsuranceDeductibleOption = null;
                order.InsuranceClaimMode = null;
                order.InsuranceQuoteState = null;
                return;
            }

            var normalizedDeductibleOption = NormalizeToken(deductibleOption);
            var normalizedClaimMode = NormalizeToken(claimMode);
            order.InsuranceDeductibleAmount = NormalizeInsuranceDeductibleAmount(normalizedDeductibleOption, deductibleAmount);
            order.InsuranceDeductibleOption = normalizedDeductibleOption;
            order.InsuranceClaimMode = normalizedClaimMode;
            order.InsuranceQuoteState = ResolveInsuranceQuoteState(normalizedDeductibleOption, normalizedClaimMode, quoteState);
        }

        private static decimal? NormalizeInsuranceDeductibleAmount(string? deductibleOption, decimal? rawAmount)
        {
            var normalizedOption = NormalizeToken(deductibleOption);
            if (string.Equals(normalizedOption, "500K", StringComparison.OrdinalIgnoreCase))
            {
                return 500000m;
            }

            if (string.Equals(normalizedOption, "1000K", StringComparison.OrdinalIgnoreCase))
            {
                return 1000000m;
            }

            if (string.Equals(normalizedOption, "CONSULT", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return rawAmount.HasValue && rawAmount.Value >= 0m ? rawAmount.Value : null;
        }

        private static string? ResolveInsuranceQuoteState(string? deductibleOption, string? claimMode, string? rawQuoteState)
        {
            var normalizedQuoteState = NormalizeToken(rawQuoteState);
            if (!string.IsNullOrWhiteSpace(normalizedQuoteState))
            {
                return normalizedQuoteState;
            }

            var normalizedDeductibleOption = NormalizeToken(deductibleOption);
            var normalizedClaimMode = NormalizeToken(claimMode);
            if (string.Equals(normalizedDeductibleOption, "CONSULT", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedClaimMode, "ADVISOR", StringComparison.OrdinalIgnoreCase))
            {
                return "NEEDS_ADVISOR";
            }

            if (!string.IsNullOrWhiteSpace(normalizedDeductibleOption) &&
                !string.IsNullOrWhiteSpace(normalizedClaimMode))
            {
                return "PENDING_REVIEW";
            }

            return null;
        }
    }
}
