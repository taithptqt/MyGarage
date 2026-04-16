using System.Threading;
using System.Threading.Tasks;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    // Temporary no-op implementation to keep service-order workflows compiling
    // until the maintenance reminder module is implemented.
    public sealed class MaintenanceReminderService : IMaintenanceReminderService
    {
        public Task ScheduleFromServiceOrderAsync(
            long serviceOrderId,
            int? remindAfterDays,
            int? remindAfterMileage,
            long? actorUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
