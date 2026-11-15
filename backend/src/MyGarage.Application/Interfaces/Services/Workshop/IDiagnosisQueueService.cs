using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisQueueService
    {
        Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder, string? dispatchTag = null);
        Task<bool> TechnicianExistsAsync(long technicianId);
        Task<Team?> GetTeamByIdAsync(long teamId);
        Task<Staff?> GetStaffByIdAsync(long staffId);
        Task<long?> ResolveTeamIdForTechnicianAsync(long technicianId);
        Task<bool> IsTechnicianInTeamAsync(long technicianId, long teamId);
        Task<bool> IsDispatchTagCompatibleAsync(string? dispatchTag, long? teamId);
        Task<bool> IsBayInBranchAsync(long bayId, long branchId);
        Task<long?> ResolveDiagnoseBayIdForBranchAsync(long branchId, long? preferredBayId = null);
        Task<WorkSlot?> ScheduleDiagnosisQueueSlotAsync(long serviceOrderId, int diagnoseMinutes, int bufferMinutes = 15, long? preferredBayId = null);
        Task<(bool IsCompatible, string Reason)> ValidateTeamBayCompatibilityAsync(long serviceOrderId, long? teamId, long bayId);
        Task<DiagnosisDispatchSuggestionDto> BuildDispatchSuggestionAsync(long serviceOrderId, long teamId);
        Task AssignDiagnosesAsync(
            IEnumerable<Diagnosis> diagnoses,
            long technicianId,
            long? assistantTechnicianId,
            long? teamId,
            IReadOnlyList<long>? supportingTeamIds,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments,
            long? bayId,
            bool managerOverride);
        Task StartDiagnosesAsync(IEnumerable<Diagnosis> diagnoses, long technicianId, long? bayId);
        Task MarkSupportRequestedAsync(
            IEnumerable<Diagnosis> diagnoses,
            long requestedByStaffId,
            string? note,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments = null);
        Task AssignSupportingTeamSelectionAsync(
            IEnumerable<Diagnosis> diagnoses,
            long teamId,
            long teamLeaderId,
            long technicianId,
            long? assistantTechnicianId,
            string? note);
    }
}
