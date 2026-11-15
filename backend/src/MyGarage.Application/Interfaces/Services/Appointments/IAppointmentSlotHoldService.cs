using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IAppointmentSlotHoldService
    {
        Task<SlotHoldResponseDto> CreateSlotHoldAsync(SlotHoldRequestDto dto);
        Task<bool> ReleaseSlotHoldAsync(string holdToken);
        Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(long workSlotId, WorkSlotDynamicExtensionRequestDto dto, long? actorUserId = null);
    }
}
