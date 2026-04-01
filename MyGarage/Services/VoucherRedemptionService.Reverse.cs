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
            var redemption = await _context.VoucherRedemptions
                .Include(x => x.Voucher)
                .FirstOrDefaultAsync(x => x.Id == redemptionId);

            var validationError = await ValidateReverseRequestAsync(redemption, request);
            if (validationError != null) return validationError;

            var voucher = redemption!.Voucher!;
            var now = DateTime.UtcNow;
            var discountAmount = redemption.DiscountAmount;

            await using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                ApplyReverseState(redemption, request, now);
                ApplyVoucherReverseRollback(voucher, discountAmount, now, request.ApprovedBy);

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
    }
}