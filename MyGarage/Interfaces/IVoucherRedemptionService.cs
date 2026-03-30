using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherRedemptionService : IGenericService<VoucherRedemption>
    {
        Task<IEnumerable<VoucherRedemption>> GetAllAsync(string? status = null);
        Task<VoucherRedemption?> GetDetailedByIdAsync(long id);
        Task<VoucherValidationResultResponseDto> CheckEligibilityAsync(VoucherRedemptionRequestDto request);
        Task<VoucherRedeemResultResponseDto> RedeemAsync(VoucherRedemptionRequestDto request);
        Task<VoucherRedeemResultResponseDto> ReverseAsync(long redemptionId, VoucherRedemptionReverseDto request);
    }
}

