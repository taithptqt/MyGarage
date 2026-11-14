using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        public async Task<VoucherRedeemResultResponseDto> ReverseAsync(long redemptionId, VoucherRedemptionReverseDto request)
        {
            if (_context.Database.CurrentTransaction != null)
                throw new InvalidOperationException("Voucher reversal cannot run inside an existing transaction.");
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var tx = await TryBeginTransactionAsync();
                try
                {
                    var voucherId = await _context.VoucherRedemptions.AsNoTracking().Where(x => x.Id == redemptionId)
                        .Select(x => (long?)x.VoucherId).FirstOrDefaultAsync();
                    if (voucherId.HasValue) await AcquireVoucherOperationLockAsync("voucher:" + voucherId.Value);
                    var redemption = await _context.VoucherRedemptions.Include(x => x.Voucher)
                        .FirstOrDefaultAsync(x => x.Id == redemptionId);
                    var validationError = await ValidateReverseRequestAsync(redemption, request);
                    if (validationError != null) return validationError;
                    var voucher = redemption!.Voucher!;
                    var now = VietnamTime.Now;
                    var discountAmount = redemption.DiscountAmount;
                    ApplyReverseState(redemption, request, now);
                    ApplyVoucherReverseRollback(voucher, discountAmount, now, request.ApprovedBy);

                    await _context.SaveChangesAsync();
                    if (tx != null) await tx.CommitAsync();

                    return new VoucherRedeemResultResponseDto
                    {
                        Success = true,
                        Message = "Voucher redemption reversed successfully.",
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
