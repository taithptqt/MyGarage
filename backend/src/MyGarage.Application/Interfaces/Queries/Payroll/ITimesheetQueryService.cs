namespace MyGarage.Application.Interfaces.Queries;

public sealed record TimesheetEntryOwnerInfo(long StaffId, long BranchId);

public interface ITimesheetQueryService
{
    Task<TimesheetEntryOwnerInfo?> GetEntryOwnerAsync(long timesheetEntryId, CancellationToken ct = default);
    Task<long?> GetStaffBranchIdAsync(long staffId, CancellationToken ct = default);
}
