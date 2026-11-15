using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;

namespace MyGarage.Interfaces
{
    public interface IServiceOrderEditRequestService
    {
        Task<IReadOnlyList<ServiceOrderEditRequestItemDto>> GetEditRequestsAsync(long id, string? status);

        Task<ServiceOrderCommandResult> RequestEditAsync(
            long id,
            long? currentUserId,
            EditRequestCreateDto? dto);

        Task<ServiceOrderCommandResult> DecideEditRequestAsync(
            long id,
            long requestId,
            long? currentUserId,
            string? claimRoleCode,
            string? claimRoleName,
            EditRequestDecisionDto? dto);
    }
}
