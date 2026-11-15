using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Appointments;

public interface IAppointmentScheduleQueryStore
{
    Task<Appointment?> GetAppointmentByIdNoTrackingAsync(long id);
    Task<List<Appointment>> GetAppointmentsForEmailAsync(IReadOnlyCollection<long> appointmentIds);
    Task<List<Appointment>> GetOpenAppointmentsOverlappingAsync(long branchId, DateTime startAt, DateTime endAt);
    Task<int> CountActiveBranchAppointmentsAsync(long branchId, IReadOnlyCollection<string> statuses);
    Task<int> CountOverlappingAppointmentsWithStatusesAsync(long branchId, DateTime startAt, DateTime endAt, IReadOnlyCollection<string> statuses);
    Task<int> CountOverlappingNonClosedAppointmentsAsync(long branchId, DateTime startAt, DateTime endAt);
    Task<List<Appointment>> GetAutoNoShowCandidatesAsync(long branchId, DateTime maxStartTime);
    Task<string?> GetFirstFlexibleQueueVehiclePlateAsync(long branchId);
}
