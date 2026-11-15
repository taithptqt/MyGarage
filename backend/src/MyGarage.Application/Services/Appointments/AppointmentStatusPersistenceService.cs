using System;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentStatusPersistenceService : IAppointmentStatusPersistenceService
    {
        private readonly IAppointmentRepository _appointmentRepository;
        private readonly IAppointmentStore _store;
        private readonly IGroupSessionService _groupSessionService;
        private readonly ISystemClock _clock;

        public AppointmentStatusPersistenceService(
            IAppointmentRepository appointmentRepository,
            IAppointmentStore store,
            IGroupSessionService groupSessionService,
            ISystemClock clock)
        {
            _appointmentRepository = appointmentRepository;
            _store = store;
            _groupSessionService = groupSessionService;
            _clock = clock;
        }

        public DateTime LocalNow => _clock.LocalNow;

        public async Task<(Appointment Appointment, long? ActorStaffId)?> LoadStatusContextAsync(long id, long? actorUserId)
        {
            var appointment = await _appointmentRepository.GetByIdAsync(id);
            if (appointment == null) return null;

            var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
            return (appointment, actorStaffId);
        }

        public async Task<Appointment?> PersistStatusChangeAsync(long id, Appointment appointment, long? actorStaffId)
        {
            appointment.UpdatedAt = _clock.UtcNow;
            appointment.UpdatedBy = actorStaffId;
            var updated = await _appointmentRepository.UpdateAsync(id, appointment);
            await _groupSessionService.SyncCountersAsync(updated?.GroupSessionId, actorStaffId);
            return updated;
        }

        public Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
        {
            return _store.ExecuteInTransactionAsync(action);
        }
    }
}
