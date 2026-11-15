using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    public interface IAppointmentEditRequestService
    {
        Task<AppointmentsCommandResult<AppointmentEditCapabilityResponseDto>> GetEditCapabilitiesAsync(
            long id,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);

        Task<AppointmentsCommandResult<IReadOnlyList<AppointmentEditRequestItemDto>>> GetEditRequestsAsync(
            long id,
            string? status,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);

        Task<AppointmentsCommandResult> RequestEditAsync(
            long id,
            EditRequestCreateDto? dto,
            AppointmentActorContext actor);

        Task<AppointmentsCommandResult> DecideEditRequestAsync(
            long id,
            long requestId,
            EditRequestDecisionDto? dto,
            AppointmentActorContext actor,
            CancellationToken cancellationToken = default);
    }
}
