using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public sealed record BranchCoordinateInfo(long Id, string? Name, decimal Lat, decimal Lng);

public interface ITimesheetStore
{
    Task<bool> StaffExistsAsync(long staffId);
    Task<IReadOnlyList<TimesheetEntry>> GetTodayEntriesAsync(long staffId, System.DateOnly today);
    Task<TimesheetEntry?> GetActiveEntryAsync(long staffId);
    Task<Dictionary<long, BranchOperatingHour>> GetOperatingHoursByBranchIdsAsync(IReadOnlyCollection<long> branchIds, int dayOfWeek);
    Task<BranchOperatingHour?> GetOperatingHourAsync(long branchId, int dayOfWeek);
    Task<IReadOnlyList<BranchCoordinateInfo>> GetBranchCoordinatesAsync();
    Task<bool> BranchExistsAsync(long branchId);
    Task<bool> HasSameDayEntryAsync(long staffId, System.DateOnly workDate, string workType);
    Task<TimesheetEntry?> GetEntryForUpdateAsync(long timesheetId);
    Task CreateEntryInTransactionAsync(TimesheetEntry entry);
    Task SaveChangesInTransactionAsync();
}
