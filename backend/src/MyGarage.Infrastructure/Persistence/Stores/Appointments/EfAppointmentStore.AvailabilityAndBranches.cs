using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Appointments;

public sealed partial class EfAppointmentStore
{
    public async Task<BranchOperatingHour?> GetBranchOperatingHourAsync(long branchId, int dayOfWeek)
    {
        return await _context.BranchOperatingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.BranchId == branchId && h.DayOfWeek == dayOfWeek);
    }

    public async Task<BranchException?> GetBranchExceptionAsync(long branchId, DateOnly date)
    {
        return await _context.BranchExceptions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.BranchId == branchId && e.ExceptionDate == date);
    }

    public async Task<Branch?> GetActiveBranchByIdAsync(long branchId)
    {
        return await _context.Branches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId && b.IsDeleted != true);
    }

    public async Task<List<Branch>> GetActiveBranchesExceptAsync(long excludedBranchId)
    {
        return await _context.Branches
            .AsNoTracking()
            .Where(b => b.Id != excludedBranchId && b.IsDeleted != true)
            .ToListAsync();
    }

    public async Task<List<Bay>> GetActiveBaysAsync(long branchId, bool ordered = false)
    {
        var query = _context.Bays
            .AsNoTracking()
            .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true);

        if (ordered)
        {
            query = query.OrderBy(b => b.SequenceOrder).ThenBy(b => b.Code);
        }

        return await query.ToListAsync();
    }

    public async Task<int> CountActiveBaysAsync(long branchId)
    {
        return await _context.Bays
            .AsNoTracking()
            .Where(b => b.BranchId == branchId && b.IsActive == true && b.IsDeleted != true)
            .CountAsync();
    }

    public async Task<List<CapacityConfig>> GetActiveCapacityConfigsForDayAsync(long branchId, byte normalizedDayOfWeek)
    {
        return await _context.CapacityConfigs
            .AsNoTracking()
            .Where(c =>
                c.IsActive &&
                c.DayOfWeek == normalizedDayOfWeek &&
                (c.BranchId == branchId || c.BranchId == null))
            .OrderByDescending(c => c.BranchId.HasValue)
            .ThenBy(c => c.StartTime)
            .ToListAsync();
    }

    public async Task<Appointment?> GetAppointmentByIdNoTrackingAsync(long id)
    {
        return await _context.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<List<Appointment>> GetAppointmentsForEmailAsync(IReadOnlyCollection<long> appointmentIds)
    {
        if (appointmentIds.Count == 0)
        {
            return new List<Appointment>();
        }

        return await _context.Appointments
            .AsNoTracking()
            .Include(a => a.Branch)
            .Include(a => a.Customer)
            .Include(a => a.GroupSession)
            .Include(a => a.Vehicle)
                .ThenInclude(v => v!.Model)
                    .ThenInclude(m => m.Brand)
            .Where(a => appointmentIds.Contains(a.Id))
            .OrderBy(a => a.StartTime)
            .ToListAsync();
    }

    public async Task<List<Appointment>> GetOpenAppointmentsOverlappingAsync(long branchId, DateTime startAt, DateTime endAt)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.StartTime < endAt &&
                a.EndTime > startAt &&
                !AppointmentStatusGroups.Closed.Contains(a.Status))
            .ToListAsync();
    }

    public async Task<int> CountActiveBranchAppointmentsAsync(long branchId, IReadOnlyCollection<string> statuses)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a => a.BranchId == branchId && statuses.Contains(a.Status))
            .CountAsync();
    }

    public async Task<int> CountOverlappingAppointmentsWithStatusesAsync(
        long branchId,
        DateTime startAt,
        DateTime endAt,
        IReadOnlyCollection<string> statuses)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.StartTime < endAt &&
                a.EndTime > startAt &&
                statuses.Contains(a.Status))
            .CountAsync();
    }

    public async Task<int> CountOverlappingNonClosedAppointmentsAsync(long branchId, DateTime startAt, DateTime endAt)
    {
        return await _context.Appointments
            .AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.StartTime < endAt &&
                a.EndTime > startAt &&
                !AppointmentStatusGroups.Closed.Contains(a.Status))
            .CountAsync();
    }

    public void AddAppointment(Appointment appointment)
    {
        _context.Appointments.Add(appointment);
    }
}
