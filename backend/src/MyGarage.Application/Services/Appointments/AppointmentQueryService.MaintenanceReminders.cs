using System;
using System.Threading.Tasks;

namespace MyGarage.Services
{
    public partial class AppointmentQueryService
    {
        private async Task TryMarkMaintenanceReminderBookedAsync(
            long customerId,
            long? vehicleId,
            long appointmentId)
        {
            if (customerId <= 0 || !vehicleId.HasValue || vehicleId.Value <= 0 || appointmentId <= 0)
            {
                return;
            }

            var reminderId = await _store.FindPendingMaintenanceReminderIdAsync(customerId, vehicleId.Value, _clock.LocalNow);

            if (!reminderId.HasValue || reminderId.Value <= 0)
            {
                return;
            }

            try
            {
                await _maintenanceReminderService.PatchStatusAsync(
                    reminderId.Value,
                    "BOOKED",
                    customerResponse: "BOOKED_FROM_APPOINTMENT",
                    resultAppointmentId: appointmentId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to mark maintenance reminder as BOOKED for appointment {AppointmentId}.",
                    appointmentId);
            }
        }
    }
}
