using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        private const string RedemptionStatusRedeemed = nameof(VoucherRedemptionStatusCode.REDEEMED);
        private const string ValidationStatusValidated = nameof(VoucherRedemptionValidationStatusCode.VALIDATED);
        private const string VoucherStatusExhausted = nameof(VoucherStatusCode.EXHAUSTED);

        public async Task<VoucherRedeemResultResponseDto> RedeemAsync(VoucherRedemptionRequestDto request)
        {
            if (_context.Database.CurrentTransaction != null)
                throw new InvalidOperationException("Voucher redemption cannot run inside an existing transaction.");
            // Internal keys protect retry after an unknown commit result. Legacy client retries
            // without a key are separate requests and cannot be deduplicated reliably.
            var operationKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                ? "server-" + Guid.NewGuid().ToString("N") : request.IdempotencyKey.Trim();
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var tx = await TryBeginTransactionAsync();
                try
                {
                    // SQL equality for a varchar key depends on the database collation; a
                    // hash of a .NET-normalized string can map equal keys to different locks.
                    // Serialize key lookup/insertion across vouchers until the schema has a
                    // tested uniqueness constraint matching the database collation.
                    await AcquireVoucherOperationLockAsync("idempotency-keys");
                    var existing = await _voucherRedemptionRepository.GetByIdempotencyKeyAsync(operationKey);
                    if (existing != null)
                    {
                        if (existing.BranchId != request.BranchId || existing.AppliedBy != request.AppliedBy ||
                            existing.CustomerId != request.CustomerId || existing.VehicleId != request.VehicleId ||
                            existing.ServiceOrderId != request.ServiceOrderId || existing.RetailOrderId != request.RetailOrderId ||
                            existing.InvoiceId != request.InvoiceId || existing.PaymentMethodId != request.PaymentMethodId ||
                            (request.VoucherId.HasValue && existing.VoucherId != request.VoucherId.Value) ||
                            (!string.IsNullOrWhiteSpace(request.VoucherCode) && !string.Equals(existing.VoucherCode, request.VoucherCode.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                            (!request.InvoiceId.HasValue && request.EligibleAmount.HasValue && existing.EligibleAmount != request.EligibleAmount.Value))
                            return new VoucherRedeemResultResponseDto { Success = false, ReasonCode = "VCH_IDEMPOTENCY_CONFLICT", Message = "Idempotency key is already used for a different request." };
                        return new VoucherRedeemResultResponseDto { Success = string.Equals(existing.RedemptionStatus, RedemptionStatusRedeemed, StringComparison.OrdinalIgnoreCase), Message = "Duplicate request was deduplicated.", Redemption = ToResponse(existing) };
                    }
                    var identity = await _context.Vouchers.AsNoTracking()
                        .Where(v => request.VoucherId.HasValue ? v.Id == request.VoucherId.Value : v.Code == (request.VoucherCode ?? "").Trim())
                        .Select(v => (long?)v.Id).FirstOrDefaultAsync();
                    if (!identity.HasValue)
                        return new VoucherRedeemResultResponseDto { Success = false, ReasonCode = "VCH_NOT_FOUND", Message = "Voucher not found." };
                    await AcquireVoucherOperationLockAsync("voucher:" + identity.Value);
                    var validation = await CheckEligibilityAsync(request);
                    if (!validation.IsEligible)
                        return new VoucherRedeemResultResponseDto { Success = false, RequiresApproval = validation.RequiresApproval, ReasonCode = validation.ReasonCode, Message = validation.Message };
                    var voucher = await _context.Vouchers.FirstAsync(x => x.Id == identity.Value);
                    var now = VietnamTime.Now;
                    var discount = validation.DiscountPreview.GetValueOrDefault(0m);
                    var eligibleAmount = validation.EligibleAmount.GetValueOrDefault(0m);
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
                        IdempotencyKey = operationKey,
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
                    if (tx != null) await tx.CommitAsync();

                    return new VoucherRedeemResultResponseDto
                    {
                        Success = true,
                        Message = "Voucher redeemed successfully.",
                        Redemption = ToResponse(redemption)
                    };
                }
                catch
                {
                    if (tx != null) { try { await tx.RollbackAsync(); } catch { /* Preserve original failure. */ } }
                    throw;
                }
            });
        }

    }
}

