using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Exceptions;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed class EfTimesheetStore : ITimesheetStore
{
    private readonly IGarageContext _context;

    public EfTimesheetStore(IGarageContext context)
    {
        _context = context;
    }

    public Task<bool> StaffExistsAsync(long staffId)
    {
        return _context.Staffs.AsNoTracking()
            .AnyAsync(x => x.Id == staffId && x.IsDeleted != true);
    }

    public async Task<IReadOnlyList<TimesheetEntry>> GetTodayEntriesAsync(long staffId, DateOnly today)
    {
        return await _context.TimesheetEntries.AsNoTracking()
            .Where(x => x.StaffId == staffId && x.WorkDate == today)
            .OrderByDescending(x => x.CheckInTime)
            .ToListAsync();
    }

    public Task<TimesheetEntry?> GetActiveEntryAsync(long staffId)
    {
        return _context.TimesheetEntries.AsNoTracking()
            .Where(x => x.StaffId == staffId && !x.CheckOutTime.HasValue)
            .OrderByDescending(x => x.CheckInTime)
            .FirstOrDefaultAsync();
    }

    public async Task<Dictionary<long, BranchOperatingHour>> GetOperatingHoursByBranchIdsAsync(
        IReadOnlyCollection<long> branchIds,
        int dayOfWeek)
    {
        if (branchIds.Count == 0)
        {
            return new Dictionary<long, BranchOperatingHour>();
        }

        return await _context.BranchOperatingHours.AsNoTracking()
            .Where(x => branchIds.Contains(x.BranchId) && x.DayOfWeek == dayOfWeek)
            .ToDictionaryAsync(x => x.BranchId);
    }

    public Task<BranchOperatingHour?> GetOperatingHourAsync(long branchId, int dayOfWeek)
    {
        return _context.BranchOperatingHours.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BranchId == branchId && x.DayOfWeek == dayOfWeek);
    }

    public async Task<IReadOnlyList<BranchCoordinateInfo>> GetBranchCoordinatesAsync()
    {
        return await _context.Branches.AsNoTracking()
            .Where(x => x.IsDeleted != true && x.Lat.HasValue && x.Lng.HasValue)
            .Select(x => new BranchCoordinateInfo(x.Id, x.Name, x.Lat!.Value, x.Lng!.Value))
            .ToListAsync();
    }

    public Task<bool> BranchExistsAsync(long branchId)
    {
        return _context.Branches.AsNoTracking().AnyAsync(x => x.Id == branchId);
    }

    public Task<bool> HasSameDayEntryAsync(long staffId, DateOnly workDate, string workType)
    {
        return _context.TimesheetEntries.AsNoTracking().AnyAsync(x =>
            x.StaffId == staffId &&
            x.WorkDate == workDate &&
            x.WorkType == workType);
    }

    public Task<TimesheetEntry?> GetEntryForUpdateAsync(long timesheetId)
    {
        return _context.TimesheetEntries.FirstOrDefaultAsync(x => x.Id == timesheetId);
    }

    public async Task CreateEntryInTransactionAsync(TimesheetEntry entry)
    {
        try
        {
            _context.TimesheetEntries.Add(entry);
            await SaveChangesInTransactionAsync();
        }
        catch (DbUpdateException)
        {
            throw new BadRequestException("Không thể check-in do bản ghi ca làm đã tồn tại.");
        }
    }

    public async Task SaveChangesInTransactionAsync()
    {
        await ExecuteInTransactionAsync(async () =>
        {
            await _context.SaveChangesAsync();
        });
    }

    private async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            await action();
            if (tx != null)
            {
                await tx.CommitAsync();
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        return await _context.Database.BeginTransactionAsync();
    }
}
