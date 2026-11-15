using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    internal static class VoucherRedemptionHelpers
    {
        internal static decimal CalculateDiscount(Voucher voucher, decimal eligibleAmount)
        {
            if (eligibleAmount <= 0)
            {
                return 0;
            }

            decimal discount;
            var type = (voucher.Type ?? string.Empty).Trim().ToUpperInvariant();
            if (type == "PERCENTAGE")
            {
                discount = Math.Round(eligibleAmount * (voucher.Value / 100m), 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                discount = voucher.Value;
            }

            if (voucher.MaxDiscountAmount.HasValue)
            {
                discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);
            }

            discount = Math.Min(discount, eligibleAmount);
            return Math.Max(discount, 0);
        }

        internal static string MapStatusReason(string status)
        {
            return status switch
            {
                nameof(VoucherStatusCode.DRAFT) => "VCH_NOT_ACTIVE",
                nameof(VoucherStatusCode.PENDING_APPROVAL) => "VCH_PENDING_APPROVAL",
                nameof(VoucherStatusCode.SCHEDULED) => "VCH_NOT_STARTED",
                nameof(VoucherStatusCode.PAUSED) => "VCH_PAUSED",
                nameof(VoucherStatusCode.EXHAUSTED) => "VCH_QUOTA_EXHAUSTED",
                nameof(VoucherStatusCode.EXPIRED) => "VCH_EXPIRED",
                nameof(VoucherStatusCode.REVOKED) => "VCH_REVOKED",
                nameof(VoucherStatusCode.ARCHIVED) => "VCH_ARCHIVED",
                _ => "VCH_INVALID_STATUS"
            };
        }

        internal static VoucherValidationResultResponseDto BuildFail(
            string reasonCode,
            string message,
            Voucher? voucher = null,
            bool requiresApproval = false)
        {
            return new VoucherValidationResultResponseDto
            {
                IsEligible = false,
                RequiresApproval = requiresApproval,
                VoucherId = voucher?.Id,
                VoucherCode = voucher?.Code,
                ReasonCode = reasonCode,
                Message = message
            };
        }

        internal static VoucherValidationResultResponseDto BuildEligibilitySuccess(
            Voucher voucher,
            decimal eligibleAmount,
            decimal discount)
        {
            return new VoucherValidationResultResponseDto
            {
                IsEligible = true,
                RequiresApproval = false,
                VoucherId = voucher.Id,
                VoucherCode = voucher.Code,
                EligibleAmount = eligibleAmount,
                DiscountPreview = discount,
                Message = "Voucher is eligible."
            };
        }

        internal static VoucherRedemptionResponseDto ToResponse(VoucherRedemption redemption)
        {
            return new VoucherRedemptionResponseDto
            {
                Id = redemption.Id,
                VoucherId = redemption.VoucherId,
                VoucherCode = redemption.VoucherCode,
                CustomerId = redemption.CustomerId,
                VehicleId = redemption.VehicleId,
                BranchId = redemption.BranchId,
                ServiceOrderId = redemption.ServiceOrderId,
                RetailOrderId = redemption.RetailOrderId,
                InvoiceId = redemption.InvoiceId,
                PaymentMethodId = redemption.PaymentMethodId,
                ValidationStatus = redemption.ValidationStatus,
                RedemptionStatus = redemption.RedemptionStatus,
                FailReasonCode = redemption.FailReasonCode,
                EligibleAmount = redemption.EligibleAmount,
                DiscountAmount = redemption.DiscountAmount,
                BudgetImpact = redemption.BudgetImpact,
                AppliedBy = redemption.AppliedBy,
                ApprovedBy = redemption.ApprovedBy,
                ApprovalRequestId = redemption.ApprovalRequestId,
                IdempotencyKey = redemption.IdempotencyKey,
                AppliedAt = redemption.AppliedAt,
                RedeemedAt = redemption.RedeemedAt,
                ReversedAt = redemption.ReversedAt,
                ReverseReason = redemption.ReverseReason,
                CreatedAt = redemption.CreatedAt
            };
        }

        internal static HashSet<string> ParseAllowedTokens(string? raw)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            var text = raw.Trim();
            if (text.StartsWith("[", StringComparison.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                var value = item.GetString();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    result.Add(value.Trim().ToUpperInvariant());
                                }
                            }
                            else if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var n))
                            {
                                result.Add(n.ToString(CultureInfo.InvariantCulture));
                            }
                        }

                        return result;
                    }
                }
                catch
                {
                    // ignore malformed json and fallback to split parser
                }
            }

            var tokens = text.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                result.Add(token.Trim().ToUpperInvariant());
            }

            return result;
        }

        internal static bool IsInValidTimeRange(TimeOnly? validFrom, TimeOnly? validTo, TimeSpan now)
        {
            if (!validFrom.HasValue && !validTo.HasValue)
            {
                return true;
            }

            var nowTime = TimeOnly.FromTimeSpan(now);

            if (validFrom.HasValue && validTo.HasValue)
            {
                if (validFrom.Value <= validTo.Value)
                {
                    return nowTime >= validFrom.Value && nowTime <= validTo.Value;
                }

                // Range crosses midnight
                return nowTime >= validFrom.Value || nowTime <= validTo.Value;
            }

            if (validFrom.HasValue)
            {
                return nowTime >= validFrom.Value;
            }

            return nowTime <= validTo!.Value;
        }

        internal static bool IsDayAllowed(string? validDaysOfWeek, DayOfWeek currentDay)
        {
            if (string.IsNullOrWhiteSpace(validDaysOfWeek))
            {
                return true;
            }

            var tokens = validDaysOfWeek
                .Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (tokens.Count == 0)
            {
                return true;
            }

            var dayNumber = ((int)currentDay).ToString(CultureInfo.InvariantCulture);
            if (tokens.Any(t => string.Equals(t, dayNumber, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var dayCode = currentDay.ToString().Substring(0, 3).ToUpperInvariant();
            return tokens.Any(t =>
            {
                var normalized = t.ToUpperInvariant();
                return normalized == dayCode || normalized == currentDay.ToString().ToUpperInvariant();
            });
        }
    }
}
