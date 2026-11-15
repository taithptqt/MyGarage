using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentOperationalContextStore
{
    Task<ServiceOrder?> GetServiceOrderByAppointmentIdNoTrackingAsync(long appointmentId);
    Task<List<AuditLog>> GetAppointmentEditRequestLogsAsync(long appointmentId, int take = 50);
    Task<HashSet<string>> GetAppointmentNotificationLinkActionsAsync(IReadOnlyCollection<long> customerIds);
    Task<bool> HasNotificationSentSinceAsync(long customerId, string linkAction, DateTime sinceDate);
    Task<long?> FindPendingMaintenanceReminderIdAsync(long customerId, long vehicleId, DateTime now);
    Task<List<AppointmentFlaggedWorkSlotSnapshot>> GetFlaggedWorkSlotsByAppointmentIdsAsync(IReadOnlyCollection<long> appointmentIds);
}
