using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Interfaces.Stores.Workshop;

public interface IDiagnosisDispatchStaffStore
{
    Task<Staff?> GetStaffByIdAsync(long staffId);
    Task<bool> StaffExistsAsync(long technicianId);
    Task<long?> ResolveTeamIdForTechnicianAsync(long technicianId);
    Task<bool> IsTechnicianInTeamAsync(long technicianId, long teamId);
    Task<IReadOnlyList<DiagnosisTechnicianRow>> GetActiveTeamTechniciansAsync(long teamId, string activeStatus);
    Task<IReadOnlyDictionary<long, string>> GetStaffNameMapAsync(IReadOnlyCollection<long> staffIds);
    Task<long?> GetBranchFrontOfficeStaffIdAsync(long branchId, string activeStatus);
    Task<(long Id, long? TeamId)?> GetActiveStaffTeamInfoAsync(long staffId, string activeStatus);
    Task<bool> IsActiveTeamStaffAsync(long staffId, long teamId, string activeStatus);
    Task<long?> GetFirstActiveTeamStaffIdAsync(long teamId, string activeStatus);
    Task<IReadOnlyList<DiagnosisAssignmentLoadRow>> GetAssignedDiagnosisLoadRowsAsync(IReadOnlyCollection<long> technicianIds, string assignedStatus);
    Task<IReadOnlyList<DiagnosisTeamLoadRow>> GetTeamDiagnosisLoadRowsAsync(IReadOnlyCollection<long> teamIds, string assignedStatus, string pendingStatus);
}
