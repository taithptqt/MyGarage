using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;

namespace MyGarage.Interfaces;

public interface IMaintenanceReminderService
{
    Task<object> ScheduleAsync(MaintenanceReminderScheduleRequestDto request, long? actorId, CancellationToken cancellationToken = default);

    Task<object> ScheduleFromServiceOrderAsync(
        long serviceOrderId,
        int? remindAfterDays,
        int? remindAfterMileage,
        long? actorId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<object>> GetDueAsync(DateTime fromInclusive, DateTime toExclusive, CancellationToken cancellationToken = default);

    Task<object> ProcessDueAsync(int batchSize = 200, CancellationToken cancellationToken = default);

    Task<object?> PatchStatusAsync(long reminderId, string status, string? customerResponse, long? resultAppointmentId, CancellationToken cancellationToken = default);
}
