using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IMaintenanceReminderService
    {
        Task ScheduleFromServiceOrderAsync(
            long serviceOrderId,
            int? remindAfterDays,
            int? remindAfterMileage,
            long? actorUserId,
            CancellationToken cancellationToken = default);
    }
}
