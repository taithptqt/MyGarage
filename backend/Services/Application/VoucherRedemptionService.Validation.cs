using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        public async Task<VoucherValidationResultResponseDto> CheckEligibilityAsync(VoucherRedemptionRequestDto request)
        {
            var voucher = await ResolveVoucherAsync(request.VoucherId, request.VoucherCode);
            if (voucher == null)
            {
                return BuildFail("VCH_NOT_FOUND", "Voucher not found.");
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
                return BuildFail("VCH_INVOICE_AMOUNT_INVALID", "Eligible amount must be greater than 0.", voucher);
            }

            if (voucher.MinOrderValue.HasValue && eligibleAmount < voucher.MinOrderValue.Value)
            {
                return BuildFail("VCH_MIN_ORDER_NOT_MET", "Invoice does not meet minimum amount.", voucher);
            }

            var usageCheck = await ValidateUsageConstraintsAsync(voucher, request);
            if (usageCheck != null)
            {
                return usageCheck;
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

            return BuildEligibilitySuccess(voucher, eligibleAmount, discount);
        }
    }
}
