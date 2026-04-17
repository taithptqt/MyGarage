using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Services
{
    public partial class AppointmentService
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

            var normalizedStatuses = new[] { "PENDING", "SENT", "CUSTOMER_RESPONDED" };
            var now = _clock.LocalNow;
            var reminderId = await _context.MaintenanceReminders
                .AsNoTracking()
                .Where(x =>
                    x.CustomerId == customerId &&
                    x.VehicleId == vehicleId.Value &&
                    normalizedStatuses.Contains(x.Status) &&
                    x.ResultAppointmentId == null)
                .OrderByDescending(x => x.ScheduledAt <= now)
                .ThenBy(x => Math.Abs(EF.Functions.DateDiffMinute(x.ScheduledAt, now)))
                .ThenByDescending(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();

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
