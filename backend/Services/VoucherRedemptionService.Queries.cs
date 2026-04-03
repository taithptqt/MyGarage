using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class VoucherRedemptionService
    {
        public async Task<IEnumerable<VoucherRedemption>> GetAllAsync(string? status = null)
        {
            return await _voucherRedemptionRepository.GetAllAsync(status);
        }

        public async Task<VoucherRedemption?> GetDetailedByIdAsync(long id)
        {
            return await _voucherRedemptionRepository.GetDetailedByIdAsync(id);
        }


    }
}
