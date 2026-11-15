using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Interfaces;

public sealed record GroupServiceOrderStatusSnapshot(long AppointmentId, string? Status);

public interface IGroupSessionStore
{
    Task<bool> StaffExistsAsync(long staffId);
    Task AddGroupSessionAsync(GroupSession entity);
    Task<GroupSession?> GetGroupSessionForUpdateAsync(long groupSessionId);
    Task<IReadOnlyList<string>> GetAppointmentStatusesByGroupSessionIdAsync(long groupSessionId);
    Task SaveChangesAsync();

    Task<List<Appointment>> GetAppointmentsWithVehicleForUpdateAsync(long groupSessionId);
    Task<IReadOnlyList<GroupServiceOrderStatusSnapshot>> GetServiceOrderStatusesByAppointmentIdsAsync(
        IReadOnlyCollection<long> appointmentIds);
    Task AddNotificationAndSaveAsync(Notification notification);
    Task ReloadGroupSessionAsync(GroupSession group);

    Task<List<Appointment>> GetGroupAppointmentsWithDetailsAsync(long groupSessionId);
    Task<IReadOnlyDictionary<long, string>> GetStandardServiceNamesByIdsAsync(IReadOnlyCollection<long> serviceIds);
    Task<IReadOnlyDictionary<long, string>> GetPackageNamesByIdsAsync(IReadOnlyCollection<long> packageIds);

    Task<List<GroupSession>> GetGroupSessionsByCustomerAsync(long customerId);
    Task<GroupSession?> GetGroupSessionByCustomerAndIdAsync(long customerId, long groupSessionId);
    Task<List<GroupSession>> GetGroupSessionsByBranchAsync(long branchId);
    Task<GroupSession?> GetGroupSessionByBranchAndIdAsync(long branchId, long groupSessionId);
}
