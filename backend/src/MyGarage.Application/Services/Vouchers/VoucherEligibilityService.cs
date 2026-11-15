using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Vouchers;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class VoucherEligibilityService : IVoucherEligibilityService
    {
        private const string VoucherStatusActive = nameof(VoucherStatusCode.ACTIVE);

        private readonly IVoucherStore _voucherStore;

        public VoucherEligibilityService(IVoucherStore voucherStore)
        {
            _voucherStore = voucherStore;
        }

        public async Task<VoucherValidationResultResponseDto> CheckEligibilityAsync(VoucherRedemptionRequestDto request)
        {
            var voucher = await _voucherStore.ResolveVoucherForValidationAsync(request.VoucherId, request.VoucherCode);
            if (voucher == null)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_NOT_FOUND", "Voucher not found.");
            }

            var now = VietnamTime.Now;
            var stateCheck = ValidateVoucherState(voucher, now);
            if (stateCheck != null)
            {
                return stateCheck;
            }

            var branchCheck = await ValidateBranchAssignmentAsync(voucher.Id, request.BranchId);
            if (branchCheck != null)
            {
                return branchCheck;
            }

            var eligibleAmount = await ResolveEligibleAmountAsync(request);
            if (eligibleAmount <= 0)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_INVOICE_AMOUNT_INVALID", "Eligible amount must be greater than 0.", voucher);
            }

            if (voucher.MinOrderValue.HasValue && eligibleAmount < voucher.MinOrderValue.Value)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_MIN_ORDER_NOT_MET", "Invoice does not meet minimum amount.", voucher);
            }

            var usageCheck = await ValidateUsageConstraintsAsync(voucher, request);
            if (usageCheck != null)
            {
                return usageCheck;
            }

            if (!await IsPaymentMethodAllowedAsync(voucher, request.PaymentMethodId))
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_PAYMENT_METHOD_NOT_ALLOWED", "Payment method is not allowed for this voucher.", voucher);
            }

            var discount = VoucherRedemptionHelpers.CalculateDiscount(voucher, eligibleAmount);
            if (discount <= 0)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_DISCOUNT_INVALID", "Discount amount is invalid.", voucher);
            }

            if (voucher.BudgetLimit.HasValue)
            {
                var budgetUsed = voucher.BudgetUsed;
                var budgetRemaining = voucher.BudgetLimit.Value - budgetUsed;
                if (budgetRemaining < discount)
                {
                    return VoucherRedemptionHelpers.BuildFail("VCH_BUDGET_EXHAUSTED", "Voucher budget is not enough.", voucher, true);
                }
            }

            return VoucherRedemptionHelpers.BuildEligibilitySuccess(voucher, eligibleAmount, discount);
        }

        private async Task<decimal> ResolveEligibleAmountAsync(VoucherRedemptionRequestDto request)
        {
            if (request.InvoiceId.HasValue)
            {
                var invoice = await _voucherStore.GetInvoiceByIdAsync(request.InvoiceId.Value, track: false);
                return invoice?.TotalAmount ?? 0m;
            }

            return request.EligibleAmount ?? 0m;
        }

        private static VoucherValidationResultResponseDto? ValidateVoucherState(Voucher voucher, DateTime now)
        {
            if (voucher.IsDeleted == true)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_DELETED", "Voucher is deleted.");
            }

            var status = (voucher.Status ?? string.Empty).Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(status) && status != VoucherStatusActive)
            {
                return VoucherRedemptionHelpers.BuildFail(
                    VoucherRedemptionHelpers.MapStatusReason(status),
                    $"Voucher status is {status}.",
                    voucher);
            }

            if (voucher.IsActive == false)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_INACTIVE", "Voucher is inactive.", voucher);
            }

            if (now < voucher.StartDate)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_NOT_STARTED", "Voucher is not started yet.", voucher);
            }

            if (now > voucher.EndDate)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_EXPIRED", "Voucher expired.", voucher, true);
            }

            if (!VoucherRedemptionHelpers.IsInValidTimeRange(voucher.ValidFrom, voucher.ValidTo, now.TimeOfDay))
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_INVALID_TIME_RANGE", "Voucher is outside valid time range.", voucher);
            }

            if (!VoucherRedemptionHelpers.IsDayAllowed(voucher.ValidDaysOfWeek, now.DayOfWeek))
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_INVALID_DAY", "Voucher is not valid for today.", voucher);
            }

            return null;
        }

        private async Task<VoucherValidationResultResponseDto?> ValidateBranchAssignmentAsync(long voucherId, long? branchId)
        {
            var branchRules = await _voucherStore.GetBranchAssignmentsAsync(voucherId);
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
                return VoucherRedemptionHelpers.BuildFail("VCH_BRANCH_REQUIRED", "Branch is required for this voucher.");
            }

            if (excludeBranchIds.Contains(branchId.Value))
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_BRANCH_EXCLUDED", "Voucher is excluded for this branch.");
            }

            if (includeBranchIds.Count > 0 && !includeBranchIds.Contains(branchId.Value))
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_BRANCH_NOT_ALLOWED", "Voucher is not allowed for this branch.");
            }

            return null;
        }

        private async Task<VoucherValidationResultResponseDto?> ValidateUsageConstraintsAsync(
            Voucher voucher,
            VoucherRedemptionRequestDto request)
        {
            if (voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0
                && (voucher.UsedCount ?? 0) >= voucher.UsageLimit.Value)
            {
                return VoucherRedemptionHelpers.BuildFail("VCH_QUOTA_EXHAUSTED", "Voucher usage limit exhausted.", voucher, true);
            }

            if (voucher.MaxUsesPerCustomer > 0)
            {
                if (!request.CustomerId.HasValue)
                {
                    return VoucherRedemptionHelpers.BuildFail("VCH_CUSTOMER_REQUIRED", "Customer is required for this voucher.", voucher);
                }

                var customerUsage = await _voucherStore.CountCustomerRedeemedUsesAsync(voucher.Id, request.CustomerId.Value);
                if (customerUsage >= voucher.MaxUsesPerCustomer)
                {
                    return VoucherRedemptionHelpers.BuildFail("VCH_CUSTOMER_LIMIT_REACHED", "Customer usage limit reached.", voucher);
                }
            }

            if (voucher.MaxUsesPerVehicle > 0)
            {
                if (!request.VehicleId.HasValue)
                {
                    return VoucherRedemptionHelpers.BuildFail("VCH_VEHICLE_REQUIRED", "Vehicle is required for this voucher.", voucher);
                }

                var vehicleUsage = await _voucherStore.CountVehicleRedeemedUsesAsync(voucher.Id, request.VehicleId.Value);
                if (vehicleUsage >= voucher.MaxUsesPerVehicle)
                {
                    return VoucherRedemptionHelpers.BuildFail("VCH_VEHICLE_LIMIT_REACHED", "Vehicle usage limit reached.", voucher);
                }
            }

            if (voucher.FirstUseOnly == true)
            {
                if (!request.CustomerId.HasValue)
                {
                    return VoucherRedemptionHelpers.BuildFail("VCH_CUSTOMER_REQUIRED", "Customer is required for first-use voucher.", voucher);
                }

                var hasPriorOrder = await _voucherStore.HasCustomerPriorOrderAsync(
                    request.CustomerId.Value,
                    request.ServiceOrderId,
                    request.RetailOrderId);

                if (hasPriorOrder)
                {
                    return VoucherRedemptionHelpers.BuildFail("VCH_FIRST_USE_ONLY", "Voucher is only for first use customers.", voucher);
                }
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

            var allowedTokens = VoucherRedemptionHelpers.ParseAllowedTokens(voucher.AllowedPaymentMethods);
            if (allowedTokens.Count == 0)
            {
                return true;
            }

            var idToken = paymentMethodId.Value.ToString(CultureInfo.InvariantCulture);
            if (allowedTokens.Contains(idToken))
            {
                return true;
            }

            var methodCode = await _voucherStore.GetPaymentMethodCodeAsync(paymentMethodId.Value);
            if (methodCode == null)
            {
                return false;
            }

            return allowedTokens.Contains(methodCode.Trim().ToUpperInvariant());
        }
    }
}
