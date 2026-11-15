using System;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IAppointmentStatusPersistenceService
    {
        DateTime LocalNow { get; }
        Task<(Appointment Appointment, long? ActorStaffId)?> LoadStatusContextAsync(long id, long? actorUserId);
        Task<Appointment?> PersistStatusChangeAsync(long id, Appointment appointment, long? actorStaffId);
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
    }
}
