using MyGarage.Constants;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private static ServiceCluster ResolveFlowByRequestedServices(string? requestedServiceIds, string? requestedPackageIds, string? notes)
        {
            if (!string.IsNullOrWhiteSpace(notes) && notes.Contains(AppointmentNoteMarkers.ClusterHeavy, StringComparison.OrdinalIgnoreCase))
                return ServiceCluster.Heavy;
            if (!string.IsNullOrWhiteSpace(notes) && notes.Contains(AppointmentNoteMarkers.ClusterQuick, StringComparison.OrdinalIgnoreCase))
                return ServiceCluster.Quick;
            if (IsHeavyHint(notes)) return ServiceCluster.Heavy;

            return string.IsNullOrWhiteSpace(requestedServiceIds) && string.IsNullOrWhiteSpace(requestedPackageIds)
                ? ServiceCluster.Heavy
                : ServiceCluster.Quick;
        }
    }
}
