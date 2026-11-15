using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
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
            if (request.InvoiceId.HasValue)
            {
                var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.InvoiceId.Value);
                return invoice?.TotalAmount ?? 0m;
            }

            return request.EligibleAmount ?? 0m;
        }
    }
}
