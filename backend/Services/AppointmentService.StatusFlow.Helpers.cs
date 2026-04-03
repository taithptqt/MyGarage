using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task<(Appointment appointment, long? actorStaffId)?> LoadStatusContextAsync(long id, long? actorUserId)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;

            var actorStaffId = await _bookingGroupService.ResolveStaffActorIdAsync(actorUserId);
            return (appointment, actorStaffId);
        }

        private async Task<Appointment?> PersistStatusChangeAsync(long id, Appointment appointment, long? actorStaffId)
        {
            ApplyUpdateAudit(appointment, actorStaffId);
            var updated = await _appointmentRepository.UpdateAsync(id, appointment);
            await _bookingGroupService.SyncCountersAsync(updated?.BookingGroupId, actorStaffId);
            return updated;
        }
    }
}
