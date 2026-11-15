using System;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentNoShowService
    {
        Task<int> AutoMarkNoShowsAsync(long branchId, DateTime? now = null);
        int ResolveCancelAfterMinutes(Appointment appointment, DateTime referenceTime);
        Task IncrementNoShowCountAsync(long customerId, int delta = 1);
        Task ReleaseBookingReservationArtifactsAsync(
            long appointmentId,
            long branchId,
            long? actorStaffId,
            DateTime releasedAt);
        Task ReleaseInventoryQuantityAsync(long branchId, long productId, decimal quantityToRelease);
    }
}
