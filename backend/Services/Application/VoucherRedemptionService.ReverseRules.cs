using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        private async Task<VoucherRedeemResultResponseDto?> ValidateReverseRequestAsync(
            VoucherRedemption? redemption,
            VoucherRedemptionReverseDto request)
        {
            if (redemption == null)
            {
                return new VoucherRedeemResultResponseDto
                {
                    Success = false,
                    ReasonCode = "VCH_REDEMPTION_NOT_FOUND",
                    Message = "Redemption not found."
                };
            }

            if (!string.Equals(redemption.RedemptionStatus, nameof(VoucherRedemptionStatusCode.REDEEMED), StringComparison.OrdinalIgnoreCase))
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
                if (invoice != null &&
                    string.Equals(invoice.PaymentStatus, nameof(PaymentDocumentStatusCode.PAID), StringComparison.OrdinalIgnoreCase) &&
                    !request.ApprovalRequestId.HasValue)
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

            return null;
        }

        private static void ApplyReverseState(VoucherRedemption redemption, VoucherRedemptionReverseDto request, DateTime now)
        {
            redemption.RedemptionStatus = nameof(VoucherRedemptionStatusCode.REVERSED);
            redemption.ValidationStatus = nameof(VoucherValidationStatusCode.VALIDATED);
            redemption.FailReasonCode = null;
            redemption.ReversedAt = now;
            redemption.ReverseReason = request.Reason?.Trim();
            redemption.ApprovedBy = request.ApprovedBy;
            redemption.ApprovalRequestId = request.ApprovalRequestId;
        }

        private static void ApplyVoucherReverseRollback(Voucher voucher, decimal discountAmount, DateTime now, long? approvedBy)
        {
            voucher.UsedCount = Math.Max(0, (voucher.UsedCount ?? 0) - 1);
            voucher.RedeemedCount = Math.Max(0, voucher.RedeemedCount - 1);
            voucher.BudgetUsed = Math.Max(0, voucher.BudgetUsed - discountAmount);
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = approvedBy;

            if (!string.Equals(voucher.Status, nameof(VoucherStatusCode.EXHAUSTED), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var exhaustedByUsage = voucher.UsageLimit.HasValue && voucher.UsageLimit.Value > 0 && voucher.UsedCount >= voucher.UsageLimit.Value;
            var exhaustedByBudget = voucher.BudgetLimit.HasValue && voucher.BudgetUsed >= voucher.BudgetLimit.Value;
            if (exhaustedByUsage || exhaustedByBudget)
            {
                return;
            }

            if (now > voucher.EndDate)
            {
                voucher.Status = nameof(VoucherStatusCode.EXPIRED);
                voucher.IsActive = false;
            }
            else if (now < voucher.StartDate)
            {
                voucher.Status = nameof(VoucherStatusCode.SCHEDULED);
                voucher.IsActive = true;
            }
            else
            {
                voucher.Status = nameof(VoucherStatusCode.ACTIVE);
                voucher.IsActive = true;
            }
        }
    }
}
