using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentPartReservationService
    {
        Task<PartAvailabilityEvaluation> EvaluatePartAvailabilityForBookingAsync(
            long branchId,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds = null);

        Task ApplyOrMarkPartsReservationAsync(
            Appointment appointment,
            WorkSlot partReservationSlot,
            long? vehicleModelId,
            string? requestedServiceIds,
            string? requestedPackageIds,
            long? actorStaffId,
            PartAvailabilityEvaluation partAvailability);
    }
}
