using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public sealed record AppointmentOwnerBranchInfo(long CustomerId, long BranchId);

public sealed record EditRequestBranchInfo(long? BranchId);

public interface IAppointmentAuthorizationStore
{
    Task<AppointmentOwnerBranchInfo?> GetAppointmentOwnerAndBranchAsync(long appointmentId, CancellationToken ct = default);
    Task<EditRequestBranchInfo?> GetEditRequestBranchAsync(long requestId, CancellationToken ct = default);
}
