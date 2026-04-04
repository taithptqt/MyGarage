using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        private async Task<VoucherValidationResultResponseDto?> ValidateBranchAssignmentAsync(long voucherId, long? branchId)
        {
            var branchRules = await _context.VoucherAssignments
                .Where(x => x.VoucherId == voucherId && x.AssignmentType == "BRANCH")
                .ToListAsync();

            if (branchRules.Count == 0)
            {
                return null;
            }

            var includeBranchIds = branchRules
                .Where(x => x.IsInclude == true)
                .Select(x => x.TargetId)
                .ToHashSet();
            var excludeBranchIds = branchRules
                .Where(x => x.IsInclude != true)
                .Select(x => x.TargetId)
                .ToHashSet();

            if (!branchId.HasValue)
            {
                return BuildFail("VCH_BRANCH_REQUIRED", "Branch is required for this voucher.");
            }

            if (excludeBranchIds.Contains(branchId.Value))
            {
                return BuildFail("VCH_BRANCH_EXCLUDED", "Voucher is excluded for this branch.");
            }

            if (includeBranchIds.Count > 0 && !includeBranchIds.Contains(branchId.Value))
            {
                return BuildFail("VCH_BRANCH_NOT_ALLOWED", "Voucher is not allowed for this branch.");
            }

            return null;
        }

        private async Task<bool> IsPaymentMethodAllowedAsync(Voucher voucher, long? paymentMethodId)
        {
            if (string.IsNullOrWhiteSpace(voucher.AllowedPaymentMethods))
            {
                return true;
            }

            if (!paymentMethodId.HasValue)
            {
                return false;
            }

            var allowedTokens = ParseAllowedTokens(voucher.AllowedPaymentMethods);
            if (allowedTokens.Count == 0)
            {
                return true;
            }

            var idToken = paymentMethodId.Value.ToString(CultureInfo.InvariantCulture);
            if (allowedTokens.Contains(idToken))
            {
                return true;
            }

            var paymentMethod = await _context.PaymentMethods.FirstOrDefaultAsync(x => x.Id == paymentMethodId.Value);
            if (paymentMethod?.MethodCode == null)
            {
                return false;
            }

            return allowedTokens.Contains(paymentMethod.MethodCode.Trim().ToUpperInvariant());
        }

        private static HashSet<string> ParseAllowedTokens(string? raw)
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
    }
}
