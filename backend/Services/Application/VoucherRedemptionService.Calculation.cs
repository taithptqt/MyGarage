using System;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
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
