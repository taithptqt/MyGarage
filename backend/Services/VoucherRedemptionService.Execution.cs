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
        private const string RedemptionStatusRedeemed = "REDEEMED";
        private const string ValidationStatusValidated = "VALIDATED";
        private const string VoucherStatusExhausted = "EXHAUSTED";

        public async Task<VoucherRedeemResultResponseDto> RedeemAsync(VoucherRedemptionRequestDto request)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existing = await _voucherRedemptionRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey.Trim());
                if (existing != null)
                {
                    return new VoucherRedeemResultResponseDto
                    {
                        Success = string.Equals(existing.RedemptionStatus, RedemptionStatusRedeemed, StringComparison.OrdinalIgnoreCase),
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
                    ValidationStatus = ValidationStatusValidated,
                    RedemptionStatus = RedemptionStatusRedeemed,
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
                    voucher.Status = VoucherStatusExhausted;
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

    }
}

