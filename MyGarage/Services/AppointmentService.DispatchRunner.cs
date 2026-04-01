using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        public async Task<DispatchCycleResponseDto> RunDispatchCycleAsync(long branchId, DateTime? at = null)
        {
            var executedAt = at ?? _clock.LocalNow;
            var noShows = await AutoMarkNoShowsAsync(branchId, executedAt);
            var health = await GetBranchBookingHealthAsync(branchId, executedAt);
            var delayNotifications = 0;

            if (health.IsBookingLocked)
            {
                delayNotifications = await SendDelayNotificationsAsync(branchId, executedAt, health);
            }

            return new DispatchCycleResponseDto
            {
                BranchId = branchId,
                ExecutedAt = executedAt,
                AutoNoShowCount = noShows,
                DelayNotificationCount = delayNotifications,
                BookingHealth = health
            };
        }
    }
}
