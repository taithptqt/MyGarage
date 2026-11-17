using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Persistence.Stores.Workshop;

public sealed partial class EfDiagnosisStore
{
    public Task<DiagnosisTeamProfileRow?> GetTeamProfileAsync(long teamId)
    {
        return _context.Teams
            .Where(x => x.Id == teamId)
            .Select(x => new DiagnosisTeamProfileRow(x.SpecialtyTag, x.Code))
            .FirstOrDefaultAsync();
    }

    public Task<DiagnosisDispatchTeamRow?> GetDispatchTeamByIdAsync(long teamId)
    {
        return _context.Teams.AsNoTracking()
            .Where(x => x.Id == teamId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
            .Select(x => new DiagnosisDispatchTeamRow(x.Id, x.Name, x.Code, x.SpecialtyTag, x.BranchId))
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<DiagnosisDispatchTeamRow>> GetTeamsByBranchNoTrackingAsync(long branchId)
    {
        return await _context.Teams.AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Select(x => new DiagnosisDispatchTeamRow(x.Id, x.Name, x.Code, x.SpecialtyTag, x.BranchId))
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<long, string>> GetTeamNameMapAsync(IReadOnlyCollection<long> teamIds)
    {
        if (teamIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.Teams.AsNoTracking()
            .Where(x => teamIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);
    }

    public Task<string?> GetTeamSpecialtyTagAsync(long teamId)
    {
        return _context.Teams.AsNoTracking()
            .Where(x => x.Id == teamId)
            .Select(x => x.SpecialtyTag)
            .FirstOrDefaultAsync();
    }

    public Task<long?> GetTeamLeaderIdAsync(long teamId)
    {
        return _context.Teams.AsNoTracking()
            .Where(x => x.Id == teamId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
            .Select(x => x.LeaderId)
            .FirstOrDefaultAsync();
    }

    public Task<Team?> GetTeamByIdAsync(long teamId)
    {
        return _context.Teams.FirstOrDefaultAsync(x => x.Id == teamId);
    }

    public Task<Staff?> GetStaffByIdAsync(long staffId)
    {
        return _context.Staffs.FirstOrDefaultAsync(x => x.Id == staffId);
    }

    public Task<bool> StaffExistsAsync(long technicianId)
    {
        return _context.Staffs.AnyAsync(x => x.Id == technicianId);
    }

    public Task<long?> ResolveTeamIdForTechnicianAsync(long technicianId)
    {
        return _context.Staffs.AsNoTracking()
            .Where(x => x.Id == technicianId)
            .Select(x => x.TeamId)
            .FirstOrDefaultAsync();
    }

    public Task<bool> IsTechnicianInTeamAsync(long technicianId, long teamId)
    {
        return _context.Staffs.AsNoTracking().AnyAsync(x => x.Id == technicianId && x.TeamId == teamId);
    }

    public async Task<IReadOnlyList<DiagnosisTechnicianRow>> GetActiveTeamTechniciansAsync(long teamId, string activeStatus)
    {
        return await _context.Staffs.AsNoTracking()
            .Where(x => x.TeamId == teamId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == activeStatus)
            .Select(x => new DiagnosisTechnicianRow(x.Id, x.FullName, x.SkillLevel))
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<long, string>> GetStaffNameMapAsync(IReadOnlyCollection<long> staffIds)
    {
        if (staffIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.Staffs.AsNoTracking()
            .Where(x => staffIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName })
            .ToDictionaryAsync(x => x.Id, x => x.FullName ?? string.Empty);
    }

    public async Task<long?> GetBranchFrontOfficeStaffIdAsync(long branchId, string activeStatus)
    {
        var id = await _context.Staffs.AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == activeStatus)
            .Where(x => !x.TeamId.HasValue || x.TeamId.Value <= 0)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        return id > 0 ? id : null;
    }

    public async Task<(long Id, long? TeamId)?> GetActiveStaffTeamInfoAsync(long staffId, string activeStatus)
    {
        var row = await _context.Staffs.AsNoTracking()
            .Where(x => x.Id == staffId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == activeStatus)
            .Select(x => new { x.Id, x.TeamId })
            .FirstOrDefaultAsync();

        return row == null ? null : (row.Id, row.TeamId);
    }

    public Task<bool> IsActiveTeamStaffAsync(long staffId, long teamId, string activeStatus)
    {
        return _context.Staffs.AsNoTracking()
            .AnyAsync(x =>
                x.Id == staffId &&
                x.TeamId == teamId &&
                (!x.IsDeleted.HasValue || x.IsDeleted.Value == false) &&
                (string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == activeStatus));
    }

    public async Task<long?> GetFirstActiveTeamStaffIdAsync(long teamId, string activeStatus)
    {
        var id = await _context.Staffs.AsNoTracking()
            .Where(x => x.TeamId == teamId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == activeStatus)
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        return id > 0 ? id : null;
    }

    public async Task<IReadOnlyList<DiagnosisAssignmentLoadRow>> GetAssignedDiagnosisLoadRowsAsync(
        IReadOnlyCollection<long> technicianIds,
        string assignedStatus)
    {
        if (technicianIds.Count == 0)
        {
            return Array.Empty<DiagnosisAssignmentLoadRow>();
        }

        return await (
            from assignment in _context.DiagnosisAssignments.AsNoTracking()
            join diagnosis in _context.Diagnoses.AsNoTracking() on assignment.DiagnosisId equals diagnosis.Id
            where assignment.Status == assignedStatus
            where
                (assignment.TechnicianId.HasValue && technicianIds.Contains(assignment.TechnicianId.Value)) ||
                (assignment.AssistantTechnicianId.HasValue && technicianIds.Contains(assignment.AssistantTechnicianId.Value))
            select new DiagnosisAssignmentLoadRow(
                assignment.DiagnosisId,
                diagnosis.ServiceOrderId,
                assignment.TechnicianId,
                assignment.AssistantTechnicianId,
                assignment.CreatedAt,
                assignment.Id))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisTeamLoadRow>> GetTeamDiagnosisLoadRowsAsync(
        IReadOnlyCollection<long> teamIds,
        string assignedStatus,
        string pendingStatus)
    {
        if (teamIds.Count == 0)
        {
            return Array.Empty<DiagnosisTeamLoadRow>();
        }

        return await (
            from assignment in _context.DiagnosisAssignments.AsNoTracking()
            join diagnosis in _context.Diagnoses.AsNoTracking() on assignment.DiagnosisId equals diagnosis.Id
            where assignment.AssignedTeamId.HasValue && teamIds.Contains(assignment.AssignedTeamId.Value)
            where assignment.Status == assignedStatus || assignment.Status == pendingStatus
            select new DiagnosisTeamLoadRow(
                assignment.DiagnosisId,
                diagnosis.ServiceOrderId,
                assignment.AssignedTeamId!.Value,
                assignment.CreatedAt,
                assignment.Id))
            .ToListAsync();
    }

    public Task<bool> IsBayInBranchAsync(long bayId, long branchId)
    {
        return _context.Bays.AsNoTracking()
            .AnyAsync(x => x.Id == bayId && x.BranchId == branchId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false));
    }

    public Task<long?> FindValidConfiguredBayIdAsync(
        long bayId,
        long branchId,
        bool hasConfiguredCodes,
        IReadOnlyCollection<string> configuredCodes)
    {
        return _context.Bays.AsNoTracking()
            .Where(x => x.Id == bayId && x.BranchId == branchId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Where(x => x.IsActive == true)
            .Where(x => !hasConfiguredCodes || configuredCodes.Contains((x.Code ?? string.Empty).Trim().ToUpper()))
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<DiagnosisBayRow>> GetActiveBranchBaysAsync(long branchId)
    {
        return await _context.Bays.AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Where(x => x.IsActive == true)
            .Select(x => new DiagnosisBayRow(
                x.Id,
                x.Code,
                x.Name,
                x.BayType,
                x.CurrentOccupiedVehicleId,
                x.OperationalStatus))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DiagnosisBayIdCodeNameRow>> GetActiveDiagnoseBaysAsync(long branchId, string bayTypeSpDiagnose)
    {
        return await _context.Bays.AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
            .Where(x => x.IsActive == true)
            .Where(x => x.BayType == bayTypeSpDiagnose)
            .OrderBy(x => x.Code ?? x.Name ?? string.Empty)
            .ThenBy(x => x.Id)
            .Select(x => new DiagnosisBayIdCodeNameRow(x.Id, x.Code, x.Name))
            .ToListAsync();
    }

    public Task<DiagnosisBayDetailRow?> GetBayDetailByIdAsync(long bayId)
    {
        return _context.Bays.AsNoTracking()
            .Where(x => x.Id == bayId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
            .Select(x => new DiagnosisBayDetailRow(x.Id, x.Code, x.Name, x.BranchId, x.BayType))
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<DiagnosisBayAvailabilityRow>> GetActiveBranchBayAvailabilityAsync(long branchId)
    {
        return await _context.Bays.AsNoTracking()
            .Where(x => x.BranchId == branchId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
            .Where(x => x.IsActive == true)
            .Select(x => new DiagnosisBayAvailabilityRow(x.BayType, x.CurrentOccupiedVehicleId, x.OperationalStatus))
            .ToListAsync();
    }

    public async Task<IReadOnlyDictionary<long, string>> GetBayCodeMapAsync(IReadOnlyCollection<long> bayIds)
    {
        if (bayIds.Count == 0)
        {
            return new Dictionary<long, string>();
        }

        return await _context.Bays.AsNoTracking()
            .Where(x => bayIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Code, x.Name })
            .ToDictionaryAsync(x => x.Id, x => string.IsNullOrWhiteSpace(x.Code) ? (x.Name ?? string.Empty) : x.Code);
    }

    public async Task<IReadOnlyList<WorkSlot>> GetDiagnoseWorkSlotsForOrdersAsync(IReadOnlyCollection<long> serviceOrderIds)
    {
        if (serviceOrderIds.Count == 0)
        {
            return Array.Empty<WorkSlot>();
        }

        return await _context.WorkSlots
            .Where(s =>
                s.ServiceOrderId.HasValue &&
                serviceOrderIds.Contains(s.ServiceOrderId.Value) &&
                (s.SlotType == nameof(WorkSlotType.DIAGNOSE) || s.SequenceOrder <= 1) &&
                (s.Status == nameof(WorkSlotStatus.ARRIVED) ||
                 s.Status == nameof(WorkSlotStatus.RESERVED) ||
                 s.Status == nameof(WorkSlotStatus.LOCKED)))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<WorkSlot>> GetActiveDiagnoseWorkSlotsForOrderAsync(long serviceOrderId)
    {
        return await _context.WorkSlots
            .Where(s =>
                s.ServiceOrderId == serviceOrderId &&
                (s.SlotType == nameof(WorkSlotType.DIAGNOSE) || s.SequenceOrder <= 1) &&
                s.Status != nameof(WorkSlotStatus.CANCELED) &&
                s.Status != nameof(WorkSlotStatus.NOSHOW) &&
                s.Status != nameof(WorkSlotStatus.COMPLETED))
            .ToListAsync();
    }

    public Task<WorkSlot?> GetLatestActiveDiagnoseWorkSlotAsync(long serviceOrderId)
    {
        return _context.WorkSlots
            .Where(x => x.ServiceOrderId == serviceOrderId)
            .Where(x => x.SlotType == nameof(WorkSlotType.DIAGNOSE))
            .Where(x =>
                x.Status != nameof(WorkSlotStatus.CANCELED) &&
                x.Status != nameof(WorkSlotStatus.NOSHOW) &&
                x.Status != nameof(WorkSlotStatus.COMPLETED))
            .OrderByDescending(x => x.ActualStartTime ?? x.StartTime)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<DiagnosisBlockingWorkSlotRow>> GetBlockingWorkSlotsForBaysAsync(
        IReadOnlyCollection<long> bayIds,
        long excludeSlotId)
    {
        if (bayIds.Count == 0)
        {
            return Array.Empty<DiagnosisBlockingWorkSlotRow>();
        }

        return await _context.WorkSlots.AsNoTracking()
            .Where(x => x.BayId.HasValue && bayIds.Contains(x.BayId.Value))
            .Where(x => x.Id != excludeSlotId)
            .Where(x =>
                x.Status != nameof(WorkSlotStatus.CANCELED) &&
                x.Status != nameof(WorkSlotStatus.NOSHOW) &&
                x.Status != nameof(WorkSlotStatus.COMPLETED) &&
                x.Status != nameof(WorkSlotStatus.DIAGNOSED_WAITING))
            .Select(x => new DiagnosisBlockingWorkSlotRow(x.BayId!.Value, x.StartTime, x.EndTime))
            .ToListAsync();
    }

    public Task<DiagnosisNextBookingRow?> GetNextOpenAppointmentAsync(
        long branchId,
        DateTime checkInTime,
        IReadOnlyCollection<string> closedStatuses)
    {
        return _context.Appointments.AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.StartTime >= checkInTime &&
                !closedStatuses.Contains(a.Status))
            .OrderBy(a => a.StartTime)
            .Select(a => new DiagnosisNextBookingRow(a.Id, a.StartTime))
            .FirstOrDefaultAsync();
    }
}
