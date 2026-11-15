using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Common;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Infrastructure.Queries;

public sealed class EfStaffSpecialtyDataService(IGarageContext context) : IStaffSpecialtyDataService
{
    public async Task<StaffSpecialtyUpdateResult?> UpdateSpecialtiesAsync(
        long id,
        IReadOnlyList<string>? specialties,
        long? updatedBy,
        CancellationToken ct = default)
    {
        var staff = await context.Staffs.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true, ct);
        if (staff == null)
        {
            return null;
        }

        var normalizedSpecialties = (specialties ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.Length <= 100)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        staff.Specialty = normalizedSpecialties.Count == 0
            ? null
            : string.Join(",", normalizedSpecialties);
        staff.UpdatedAt = VietnamTime.Now;
        staff.UpdatedBy = updatedBy;

        await context.SaveChangesAsync(ct);

        return new StaffSpecialtyUpdateResult(staff.Id, staff.Specialty, normalizedSpecialties);
    }

    public async Task<IReadOnlyList<StaffAvailabilityResponseDto>> GetAvailableStaffAsync(
        string? specialty,
        DateTime? date,
        long? branchId,
        CancellationToken ct = default)
    {
        var requestedDate = (date ?? VietnamTime.Now).Date;
        var dayStart = requestedDate;
        var dayEnd = requestedDate.AddDays(1);
        var normalizedSpecialty = string.IsNullOrWhiteSpace(specialty) ? string.Empty : specialty.Trim();

        var staffQuery = context.Staffs
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && (x.Status == null || x.Status != "INACTIVE"));

        if (branchId.HasValue && branchId.Value > 0)
        {
            staffQuery = staffQuery.Where(x => x.BranchId == branchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSpecialty))
        {
            staffQuery = staffQuery.Where(x =>
                x.Specialty != null &&
                EF.Functions.Like("," + x.Specialty + ",", "%," + normalizedSpecialty + ",%"));
        }

        var staffRows = await staffQuery
            .Select(x => new
            {
                x.Id,
                x.FullName,
                x.BranchId,
                x.TeamId,
                x.Specialty
            })
            .ToListAsync(ct);

        var assignedCounts = await context.WorkSlots
            .AsNoTracking()
            .Where(x =>
                x.TechnicianId.HasValue &&
                x.StartTime < dayEnd &&
                x.EndTime > dayStart &&
                x.Status != "CANCELED")
            .GroupBy(x => x.TechnicianId!.Value)
            .Select(g => new
            {
                TechnicianId = g.Key,
                Count = g.Count()
            })
            .ToDictionaryAsync(x => x.TechnicianId, x => x.Count, ct);

        return staffRows
            .Select(x =>
            {
                assignedCounts.TryGetValue(x.Id, out var taskCount);
                return new StaffAvailabilityResponseDto
                {
                    StaffId = x.Id,
                    FullName = x.FullName ?? string.Empty,
                    BranchId = x.BranchId,
                    TeamId = x.TeamId,
                    Specialty = x.Specialty,
                    AssignedTaskCountInDate = taskCount,
                    IsBusyInDate = taskCount > 0
                };
            })
            .OrderBy(x => x.IsBusyInDate)
            .ThenBy(x => x.AssignedTaskCountInDate)
            .ThenBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
