using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IVoucherReversalService
    {
        Task<VoucherRedeemResultResponseDto> ReverseAsync(long redemptionId, VoucherRedemptionReverseDto request);
    }
}
