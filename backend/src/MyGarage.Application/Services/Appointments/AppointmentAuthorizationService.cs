using System;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Authorization;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class AppointmentAuthorizationService : IAppointmentAuthorizationService
{
    private readonly IAppointmentAuthorizationStore _store;

    public AppointmentAuthorizationService(IAppointmentAuthorizationStore store)
    {
        _store = store;
    }

    public async Task<bool> CanAccessAppointmentAsync(long appointmentId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (appointmentId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        var meta = await _store.GetAppointmentOwnerAndBranchAsync(appointmentId, cancellationToken);
        if (meta == null)
        {
            return false;
        }

        if (string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return actor.BranchId == meta.BranchId;
        }

        return actor.UserId.Value == meta.CustomerId;
    }

    public async Task<bool> CanMutateAppointmentStatusAsync(long appointmentId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        return await CanAccessAppointmentAsync(appointmentId, actor, cancellationToken);
    }

    public async Task<bool> CanDecideEditRequestAsync(long requestId, UserActorContext actor, CancellationToken cancellationToken = default)
    {
        if (requestId <= 0 || !actor.UserId.HasValue)
        {
            return false;
        }

        if (actor.IsAdmin)
        {
            return true;
        }

        if (!string.Equals(actor.Role, "Staff", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestLog = await _store.GetEditRequestBranchAsync(requestId, cancellationToken);
        if (requestLog == null)
        {
            return false;
        }

        return requestLog.BranchId == actor.BranchId;
    }
}
