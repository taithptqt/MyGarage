// Mục đích: Kiểm tra điều kiện áp dụng voucher và xử lý giao dịch redeem/reverse.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VoucherRedemptionService : GenericService<VoucherRedemption>, IVoucherRedemptionService
    {
        private readonly IVoucherRedemptionRepository _voucherRedemptionRepository;
        private readonly GarageContext _context;

        public VoucherRedemptionService(IVoucherRedemptionRepository repository, GarageContext context)
            : base(repository)
        {
            _voucherRedemptionRepository = repository;
            _context = context;
        }

        public async Task<IEnumerable<VoucherRedemption>> GetAllAsync(string? status = null)
        {
            return await _voucherRedemptionRepository.GetAllAsync(status);
        }

        public async Task<VoucherRedemption?> GetDetailedByIdAsync(long id)
        {
            return await _voucherRedemptionRepository.GetDetailedByIdAsync(id);
        }

        public async Task<VoucherValidationResultResponseDto> CheckEligibilityAsync(VoucherRedemptionRequestDto request)
        {
            var voucher = await ResolveVoucherAsync(request.VoucherId, request.VoucherCode);
            if (voucher == null)
            {
                return BuildFail("VCH_NOT_FOUND", "Voucher not found.");
            }

            var now = DateTime.Now;

            if (voucher.IsDeleted == true)
            {
                return BuildFail("VCH_DELETED", "Voucher is deleted.");
            }

            var status = (voucher.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(status) && status != "ACTIVE")
            {
                return BuildFail(MapStatusReason(status), $"Voucher status is {status}.", voucher);
            }

            if (voucher.IsActive == false)
            {
                return BuildFail("VCH_INACTIVE", "Voucher is inactive.", voucher);
            }

            if (now < voucher.StartDate)
            {
                return BuildFail("VCH_NOT_STARTED", "Voucher is not started yet.", voucher);
            }

            if (now > voucher.EndDate)
            {
                return BuildFail("VCH_EXPIRED", "Voucher expired.", voucher, true);
            }

            if (!IsInValidTimeRange(voucher.ValidFrom, voucher.ValidTo, now.TimeOfDay))
            {
                return BuildFail("VCH_INVALID_TIME_RANGE", "Voucher is outside valid time range.", voucher);
            }

            if (!IsDayAllowed(voucher.ValidDaysOfWeek, now.DayOfWeek))
            {
                return BuildFail("VCH_INVALID_DAY", "Voucher is not valid for today.", voucher);
            }

            var branchCheck = await ValidateBranchAssignmentAsync(voucher.Id, request.BranchId);
            if (branchCheck != null)
            {
                return branchCheck;
            }

            var eligibleAmount = await ResolveEligibleAmountAsync(request);
            if (eligibleAmount <= 0)
            {
                return BuildFail("VCH_INVOICE_AMOUNT_INVALID", "Eligible amount must be greater than 0.", voucher);
            }

            if (voucher.MinOrderValue.HasValue && eligibleAmount < voucher.MinOrderValue.Value)
            {
                return BuildFail("VCH_MIN_ORDER_NOT_MET", "Invoice does not meet minimum amount.", voucher);
            }

            if (voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0
                && (voucher.UsedCount ?? 0) >= voucher.UsageLimit.Value)
            {
                return BuildFail("VCH_QUOTA_EXHAUSTED", "Voucher usage limit exhausted.", voucher, true);
            }

            if (voucher.MaxUsesPerCustomer > 0)
            {
                if (!request.CustomerId.HasValue)
                {
                    return BuildFail("VCH_CUSTOMER_REQUIRED", "Customer is required for this voucher.", voucher);
                }

                var customerUsage = await _context.VoucherRedemptions
                    .CountAsync(x => x.VoucherId == voucher.Id
                        && x.CustomerId == request.CustomerId
                        && x.RedemptionStatus == "REDEEMED");

                if (customerUsage >= voucher.MaxUsesPerCustomer)
                {
                    return BuildFail("VCH_CUSTOMER_LIMIT_REACHED", "Customer usage limit reached.", voucher);
                }
            }

            if (voucher.MaxUsesPerVehicle > 0)
            {
                if (!request.VehicleId.HasValue)
                {
                    return BuildFail("VCH_VEHICLE_REQUIRED", "Vehicle is required for this voucher.", voucher);
                }

                var vehicleUsage = await _context.VoucherRedemptions
                    .CountAsync(x => x.VoucherId == voucher.Id
                        && x.VehicleId == request.VehicleId
                        && x.RedemptionStatus == "REDEEMED");

                if (vehicleUsage >= voucher.MaxUsesPerVehicle)
                {
                    return BuildFail("VCH_VEHICLE_LIMIT_REACHED", "Vehicle usage limit reached.", voucher);
                }
            }

            if (voucher.FirstUseOnly == true)
            {
                if (!request.CustomerId.HasValue)
                {
                    return BuildFail("VCH_CUSTOMER_REQUIRED", "Customer is required for first-use voucher.", voucher);
                }

                var customerId = request.CustomerId.Value;
                var hasPriorServiceOrder = await _context.ServiceOrders.AnyAsync(x =>
                    x.CustomerId == customerId &&
                    (!request.ServiceOrderId.HasValue || x.Id != request.ServiceOrderId.Value));
                var hasPriorRetailOrder = await _context.RetailOrders.AnyAsync(x =>
                    x.CustomerId == customerId &&
                    (!request.RetailOrderId.HasValue || x.Id != request.RetailOrderId.Value));

                if (hasPriorServiceOrder || hasPriorRetailOrder)
                {
                    return BuildFail("VCH_FIRST_USE_ONLY", "Voucher is only for first use customers.", voucher);
                }
            }

            if (!await IsPaymentMethodAllowedAsync(voucher, request.PaymentMethodId))
            {
                return BuildFail("VCH_PAYMENT_METHOD_NOT_ALLOWED", "Payment method is not allowed for this voucher.", voucher);
            }

            var discount = CalculateDiscount(voucher, eligibleAmount);
            if (discount <= 0)
            {
                return BuildFail("VCH_DISCOUNT_INVALID", "Discount amount is invalid.", voucher);
            }

            if (voucher.BudgetLimit.HasValue)
            {
                var budgetUsed = voucher.BudgetUsed;
                var budgetRemaining = voucher.BudgetLimit.Value - budgetUsed;
                if (budgetRemaining < discount)
                {
                    return BuildFail("VCH_BUDGET_EXHAUSTED", "Voucher budget is not enough.", voucher, true);
                }
            }

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

        public async Task<VoucherRedeemResultResponseDto> RedeemAsync(VoucherRedemptionRequestDto request)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = await _voucherRedemptionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey.Trim());
                if (existing != null)
                {
                    return new VoucherRedeemResultResponseDto
                    {
                        Success = string.Equals(existing.RedemptionStatus, "REDEEMED", StringComparison.OrdinalIgnoreCase),
                        RequiresApproval = false,
                        Message = "Duplicate request was deduplicated.",
                        Redemption = ToResponse(existing)
                    };
                }
            }

            var validation = await CheckEligibilityAsync(request);
            if (!validation.IsEligible)
            {
                return new VoucherRedeemResultResponseDto
                {
                    Success = false,
                    RequiresApproval = validation.RequiresApproval,
                    ReasonCode = validation.ReasonCode,
                    Message = validation.Message
                };
            }

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(x => x.Id == validation.VoucherId);
            if (voucher == null)
            {
                return new VoucherRedeemResultResponseDto
                {
                    Success = false,
                    ReasonCode = "VCH_NOT_FOUND",
                    Message = "Voucher not found when redeeming."
                };
            }

            var now = DateTime.UtcNow;
            var discount = validation.DiscountPreview.GetValueOrDefault(0m);
            var eligibleAmount = validation.EligibleAmount.GetValueOrDefault(0m);

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var redemption = new VoucherRedemption
                {
                    VoucherId = voucher.Id,
                    VoucherCode = voucher.Code,
                    CustomerId = request.CustomerId,
                    VehicleId = request.VehicleId,
                    BranchId = request.BranchId,
                    ServiceOrderId = request.ServiceOrderId,
                    RetailOrderId = request.RetailOrderId,
                    InvoiceId = request.InvoiceId,
                    PaymentMethodId = request.PaymentMethodId,
                    ValidationStatus = "VALIDATED",
                    RedemptionStatus = "REDEEMED",
                    EligibleAmount = eligibleAmount,
                    DiscountAmount = discount,
                    BudgetImpact = discount,
                    AppliedBy = request.AppliedBy,
                    IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
                    AppliedAt = now,
                    RedeemedAt = now,
                    CreatedAt = now
                };

                _context.VoucherRedemptions.Add(redemption);

                voucher.UsedCount = (voucher.UsedCount ?? 0) + 1;
                voucher.RedeemedCount += 1;
                voucher.BudgetUsed += discount;
                voucher.UpdatedAt = now;
                voucher.UpdatedBy = request.AppliedBy;

                var exhaustedByUsage = voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0 && voucher.UsedCount >= voucher.UsageLimit.Value;
                var exhaustedByBudget = voucher.BudgetLimit.HasValue && voucher.BudgetUsed >= voucher.BudgetLimit.Value;
                if (exhaustedByUsage || exhaustedByBudget)
                {
                    voucher.Status = "EXHAUSTED";
                    voucher.IsActive = false;
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new VoucherRedeemResultResponseDto
                {
                    Success = true,
                    Message = "Voucher redeemed successfully.",
                    Redemption = ToResponse(redemption)
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<VoucherRedeemResultResponseDto> ReverseAsync(long redemptionId, VoucherRedemptionReverseDto request)
        {
            var redemption = await _context.VoucherRedemptions
                .Include(x => x.Voucher)
                .FirstOrDefaultAsync(x => x.Id == redemptionId);

            if (redemption == null)
            {
                return new VoucherRedeemResultResponseDto
                {
                    Success = false,
                    ReasonCode = "VCH_REDEMPTION_NOT_FOUND",
                    Message = "Redemption not found."
                };
            }

            if (!string.Equals(redemption.RedemptionStatus, "REDEEMED", StringComparison.OrdinalIgnoreCase))
            {
                return new VoucherRedeemResultResponseDto
                {
                    Success = false,
                    ReasonCode = "VCH_REVERSE_INVALID_STATUS",
                    Message = $"Cannot reverse redemption with status {redemption.RedemptionStatus}."
                };
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return new VoucherRedeemResultResponseDto
                {
                    Success = false,
                    ReasonCode = "VCH_REVERSE_REASON_REQUIRED",
                    Message = "Reverse reason is required."
                };
            }

            if (redemption.InvoiceId.HasValue)
            {
                var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == redemption.InvoiceId.Value);
                if (invoice != null && string.Equals(invoice.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase)
                    && !request.ApprovalRequestId.HasValue)
                {
                    return new VoucherRedeemResultResponseDto
                    {
                        Success = false,
                        RequiresApproval = true,
                        ReasonCode = "VCH_REVERSE_APPROVAL_REQUIRED",
                        Message = "Approval request is required to reverse after payment."
                    };
                }
            }

            if (redemption.Voucher == null)
            {
                return new VoucherRedeemResultResponseDto
                {
                    Success = false,
                    ReasonCode = "VCH_NOT_FOUND",
                    Message = "Voucher not found for this redemption."
                };
            }

            var voucher = redemption.Voucher;
            var now = DateTime.UtcNow;
            var discountAmount = redemption.DiscountAmount;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                redemption.RedemptionStatus = "REVERSED";
                redemption.ValidationStatus = "VALIDATED";
                redemption.FailReasonCode = null;
                redemption.ReversedAt = now;
                redemption.ReverseReason = request.Reason?.Trim();
                redemption.ApprovedBy = request.ApprovedBy;
                redemption.ApprovalRequestId = request.ApprovalRequestId;

                voucher.UsedCount = Math.Max(0, (voucher.UsedCount ?? 0) - 1);
                voucher.RedeemedCount = Math.Max(0, voucher.RedeemedCount - 1);
                voucher.BudgetUsed = Math.Max(0, voucher.BudgetUsed - discountAmount);
                voucher.UpdatedAt = now;
                voucher.UpdatedBy = request.ApprovedBy;

                if (string.Equals(voucher.Status, "EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                {
                    var exhaustedByUsage = voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0 && voucher.UsedCount >= voucher.UsageLimit.Value;
                    var exhaustedByBudget = voucher.BudgetLimit.HasValue && voucher.BudgetUsed >= voucher.BudgetLimit.Value;

                    if (!exhaustedByUsage && !exhaustedByBudget)
                    {
                        if (now > voucher.EndDate)
                        {
                            voucher.Status = "EXPIRED";
                            voucher.IsActive = false;
                        }
                        else if (now < voucher.StartDate)
                        {
                            voucher.Status = "SCHEDULED";
                            voucher.IsActive = true;
                        }
                        else
                        {
                            voucher.Status = "ACTIVE";
                            voucher.IsActive = true;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return new VoucherRedeemResultResponseDto
                {
                    Success = true,
                    Message = "Voucher redemption reversed successfully.",
                    Redemption = ToResponse(redemption)
                };
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private async Task<Voucher?> ResolveVoucherAsync(long? voucherId, string? voucherCode)
        {
            var query = _context.Vouchers.AsQueryable();

            if (voucherId.HasValue)
            {
                return await query.FirstOrDefaultAsync(x => x.Id == voucherId.Value);
            }

            if (!string.IsNullOrWhiteSpace(voucherCode))
            {
                var normalizedCode = voucherCode.Trim();
                return await query.FirstOrDefaultAsync(x => x.Code == normalizedCode);
            }

            return null;
        }

        private async Task<decimal> ResolveEligibleAmountAsync(VoucherRedemptionRequestDto request)
        {
            if (request.EligibleAmount.HasValue)
            {
                return request.EligibleAmount.Value;
            }

            if (request.InvoiceId.HasValue)
            {
                var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == request.InvoiceId.Value);
                return invoice?.TotalAmount ?? 0m;
            }

            return 0m;
        }

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

        private static bool IsInValidTimeRange(TimeOnly? validFrom, TimeOnly? validTo, TimeSpan now)
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

        private static bool IsDayAllowed(string? validDaysOfWeek, DayOfWeek currentDay)
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

        private static decimal CalculateDiscount(Voucher voucher, decimal eligibleAmount)
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

        private static string MapStatusReason(string status)
        {
            return status switch
            {
                "DRAFT" => "VCH_NOT_ACTIVE",
                "PENDING_APPROVAL" => "VCH_PENDING_APPROVAL",
                "SCHEDULED" => "VCH_NOT_STARTED",
                "PAUSED" => "VCH_PAUSED",
                "EXHAUSTED" => "VCH_QUOTA_EXHAUSTED",
                "EXPIRED" => "VCH_EXPIRED",
                "REVOKED" => "VCH_REVOKED",
                "ARCHIVED" => "VCH_ARCHIVED",
                _ => "VCH_INVALID_STATUS"
            };
        }

        private static VoucherValidationResultResponseDto BuildFail(
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

        private static VoucherRedemptionResponseDto ToResponse(VoucherRedemption redemption)
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
    }
}
