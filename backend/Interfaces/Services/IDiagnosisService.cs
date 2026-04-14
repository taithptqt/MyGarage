using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisService : IGenericService<Diagnosis>
    {
        Task<IReadOnlyList<Diagnosis>> GetAllFilteredAsync(long? serviceOrderId, bool requestOnly, long? teamId = null);
        Task<ServiceOrder?> GetServiceOrderAsync(long serviceOrderId);
        Task<bool> HasSignedCheckInAsync(long serviceOrderId);
        Task<IReadOnlyList<Diagnosis>> GetAllByServiceOrderAsync(long serviceOrderId);
        Task<DiagnosisIntakeSuggestionDto> BuildIntakeSuggestionAsync(
            long serviceOrderId,
            string? dispatchTag,
            int? bufferMinutesOverride = null,
            string? requestedPackageCode = null);
        Task<IReadOnlyList<DiagnosisIntakePackageOptionDto>> GetIntakePackagesAsync();
        Task RemoveRecommendationsByDiagnosisIdsAsync(IEnumerable<long> diagnosisIds);
        Task RemoveDiagnosesAsync(IEnumerable<Diagnosis> diagnoses);
        Task ArchiveDiagnosesAsSupersededAsync(IEnumerable<Diagnosis> diagnoses, long? actorStaffId = null, string? auditTag = null);
        Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder, string? dispatchTag = null);
        Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(
            long serviceOrderId,
            long? technicianId,
            IReadOnlyList<string> symptoms,
            string? dispatchTag = null,
            long? assignedTeamId = null,
            long? assignedBayId = null);
        Task RecordIntakeSnapshotAsync(
            ServiceOrder serviceOrder,
            DiagnosisIntakeSuggestionDto intakeSuggestion,
            string? dispatchTag,
            bool bypassConflictGuard,
            bool autoAssign,
            bool customerConsentAccepted,
            string? customerConsentChannel,
            string? customerConsentNote,
            IReadOnlyList<Diagnosis> diagnoses);
        Task<DiagnosisHandoverSnapshotResponseDto> CreateHandoverSnapshotAsync(long serviceOrderId, DiagnosisHandoverSnapshotCreateDto request);
        Task<DiagnosisHandoverSnapshotResponseDto?> GetLatestHandoverSnapshotAsync(long serviceOrderId);
        Task<DiagnosisHandoverSnapshotResponseDto> ApplyHandoverCustomerDecisionAsync(
            long serviceOrderId,
            long snapshotId,
            DiagnosisHandoverDecisionCreateDto request);
        Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(long? branchId, DateTime? startDate, DateTime? endDate);
        Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status);
        Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId);
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
        Task AssignDiagnosesAsync(
            IEnumerable<Diagnosis> diagnoses,
            long technicianId,
            long? assistantTechnicianId,
            long? teamId,
            IReadOnlyList<long>? supportingTeamIds,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments,
            long? bayId,
            bool managerOverride,
            long? assignedByTeamLeaderId = null);
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
        Task RequestCancellationAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string reason);
        Task ApplyCancellationDecisionAsync(ServiceOrder serviceOrder, IEnumerable<Diagnosis> diagnoses, bool approve, long managerId, string? note);
        Task<IReadOnlyList<DiagnosisDispatchBoardItemDto>> GetDispatchBoardAsync(long? branchId, long? teamId, bool includeCompleted);
        Task<DiagnosisDispatchSuggestionDto> BuildDispatchSuggestionAsync(long serviceOrderId, long teamId);
        Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses);
        Task<Diagnosis?> GetDiagnosisEntityAsync(long id);
        Task CompleteDiagnosisAsync(Diagnosis diagnosis);
        Task ApproveTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? note);
        Task ApproveFinalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? note);
        Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId);
    }
}
