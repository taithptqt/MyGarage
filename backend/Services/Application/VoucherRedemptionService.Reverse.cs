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
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                var redemption = await _context.VoucherRedemptions
                    .Include(x => x.Voucher)
                    .FirstOrDefaultAsync(x => x.Id == redemptionId);

                var validationError = await ValidateReverseRequestAsync(redemption, request);
                if (validationError != null) return validationError;

                var voucher = redemption!.Voucher!;
                var now = VietnamTime.Now;
                var discountAmount = redemption.DiscountAmount;

                await using var tx = await TryBeginTransactionAsync();
                try
                {
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
                    if (tx != null) await tx.RollbackAsync();
                    throw;
                }
            });
        }
    }
}
