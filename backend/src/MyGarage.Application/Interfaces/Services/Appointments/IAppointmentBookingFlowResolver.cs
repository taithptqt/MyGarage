using System.Threading.Tasks;
using MyGarage.Models;
using MyGarage.Services;

namespace MyGarage.Interfaces
{
    public interface IAppointmentBookingFlowResolver
    {
        Task<BookingFlowType> ResolveFlowAsync(
            string? bookingFlow,
            string? requestedServiceIds,
            string? requestedPackageIds = null);

        BookingFlowType ResolveFlowByAppointment(Appointment appointment);

        Task<string?> ResolveRequestedServiceIdsForBookingAsync(
            string? requestedServiceIds,
            string? requestedPackageIds = null);

        Task<int> ResolveScheduledDurationAsync(
            string? requestedServiceIds,
            int slotStepMinutes,
            bool isParallel = false,
            string? requestedPackageIds = null);

        int ResolveFlexibleWindowMinutes(int slotStepMinutes);

        Task<ServiceCluster> ResolveServiceClusterAsync(
            string? requestedServiceIds,
            string? requestedPackageIds,
            string? notes);
    }
}
