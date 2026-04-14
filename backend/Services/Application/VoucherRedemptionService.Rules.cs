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
            if (request.EligibleAmount.HasValue)
            {
                return decimal.Round(System.Math.Max(0m, request.EligibleAmount.Value), 2, System.MidpointRounding.AwayFromZero);
            }

            if (request.InvoiceId.HasValue)
            {
                var invoice = await _context.Invoices.FirstOrDefaultAsync(x => x.Id == request.InvoiceId.Value);
                return invoice?.TotalAmount ?? 0m;
            }

            if (request.ServiceOrderId.HasValue)
            {
                var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == request.ServiceOrderId.Value);
                return serviceOrder?.FinalAmount ?? 0m;
            }

            if (request.RetailOrderId.HasValue)
            {
                var retailOrder = await _context.RetailOrders.FirstOrDefaultAsync(x => x.Id == request.RetailOrderId.Value);
                return retailOrder?.FinalAmount ?? 0m;
            }

            return 0m;
        }
    }
}
