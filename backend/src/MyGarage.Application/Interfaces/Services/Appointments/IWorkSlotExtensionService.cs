using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IWorkSlotExtensionService
    {
        Task<WorkSlotDynamicExtensionResponseDto> ProcessDynamicExtensionAsync(
            long workSlotId,
            WorkSlotDynamicExtensionRequestDto dto,
            long? actorUserId = null);
    }
}
