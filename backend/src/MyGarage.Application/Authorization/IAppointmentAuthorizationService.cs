using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Application.Authorization;

public interface IAppointmentAuthorizationService
{
    Task<bool> CanAccessAppointmentAsync(long appointmentId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanMutateAppointmentStatusAsync(long appointmentId, UserActorContext actor, CancellationToken cancellationToken = default);
    Task<bool> CanDecideEditRequestAsync(long requestId, UserActorContext actor, CancellationToken cancellationToken = default);
}
