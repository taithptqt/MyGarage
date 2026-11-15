using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed class EfGroupSessionStore : IGroupSessionStore
{
    private readonly IGarageContext _context;

    public EfGroupSessionStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<bool> StaffExistsAsync(long staffId)
    {
        return _context.Staffs
            .AsNoTracking()
            .AnyAsync(s => s.Id == staffId);
    }

    public async Task AddGroupSessionAsync(GroupSession entity)
    {
        _context.GroupSessions.Add(entity);
        await _context.SaveChangesAsync();
    }

    public Task<GroupSession?> GetGroupSessionForUpdateAsync(long groupSessionId)
    {
        return _context.GroupSessions
            .FirstOrDefaultAsync(x => x.Id == groupSessionId);
    }

    public async Task<IReadOnlyList<string>> GetAppointmentStatusesByGroupSessionIdAsync(long groupSessionId)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(x => x.GroupSessionId == groupSessionId)
            .Select(x => x.Status)
            .ToListAsync();
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }

    public Task<List<Appointment>> GetAppointmentsWithVehicleForUpdateAsync(long groupSessionId)
    {
        return _context.Appointments
            .Where(a => a.GroupSessionId == groupSessionId)
            .Include(a => a.Vehicle)
            .OrderBy(a => a.StartTime)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<GroupServiceOrderStatusSnapshot>> GetServiceOrderStatusesByAppointmentIdsAsync(
        IReadOnlyCollection<long> appointmentIds)
    {
        if (appointmentIds.Count == 0)
        {
            return new List<GroupServiceOrderStatusSnapshot>();
        }

        return await _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.AppointmentId.HasValue && appointmentIds.Contains(x.AppointmentId.Value))
            .Select(x => new GroupServiceOrderStatusSnapshot(x.AppointmentId!.Value, x.Status))
            .ToListAsync();
    }

    public async Task AddNotificationAndSaveAsync(Notification notification)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }

    public Task ReloadGroupSessionAsync(GroupSession group)
    {
        return _context.Entry(group).ReloadAsync();
    }

    public Task<List<Appointment>> GetGroupAppointmentsWithDetailsAsync(long groupSessionId)
    {
        return _context.Appointments
            .AsNoTracking()
            .Include(a => a.Branch)
            .Include(a => a.Customer)
            .Include(a => a.GroupSession)
            .Include(a => a.Vehicle)
                .ThenInclude(v => v!.Model)
                    .ThenInclude(m => m.Brand)
            .Where(a => a.GroupSessionId == groupSessionId)
            .OrderBy(a => a.StartTime)
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<long, string>> GetStandardServiceNamesByIdsAsync(IReadOnlyCollection<long> serviceIds)
    {
        if (serviceIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.StandardServices
            .AsNoTracking()
            .Where(s => serviceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name ?? $"#{s.Id}");
    }

    public async Task<IReadOnlyDictionary<long, string>> GetPackageNamesByIdsAsync(IReadOnlyCollection<long> packageIds)
    {
        if (packageIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.Packages
            .AsNoTracking()
            .Where(package => packageIds.Contains(package.Id))
            .ToDictionaryAsync(package => package.Id, package => package.Name ?? $"#{package.Id}");
    }

    public Task<List<GroupSession>> GetGroupSessionsByCustomerAsync(long customerId)
    {
        return _context.GroupSessions
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public Task<GroupSession?> GetGroupSessionByCustomerAndIdAsync(long customerId, long groupSessionId)
    {
        return _context.GroupSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupSessionId && x.CustomerId == customerId);
    }

    public Task<List<GroupSession>> GetGroupSessionsByBranchAsync(long branchId)
    {
        return _context.GroupSessions
            .AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public Task<GroupSession?> GetGroupSessionByBranchAndIdAsync(long branchId, long groupSessionId)
    {
        return _context.GroupSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == groupSessionId && x.BranchId == branchId);
    }
}
