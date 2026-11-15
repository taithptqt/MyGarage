using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IVoucherEligibilityService
    {
        Task<VoucherValidationResultResponseDto> CheckEligibilityAsync(VoucherRedemptionRequestDto request);
    }
}
