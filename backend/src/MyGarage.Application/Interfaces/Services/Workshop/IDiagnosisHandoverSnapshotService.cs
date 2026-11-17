using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IDiagnosisIntakeKpiService
    {
        Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(long? branchId, DateTime? startDate, DateTime? endDate);
    }

    public interface IDiagnosisHandoverSnapshotService
    {
        Task RecordIntakeSnapshotAsync(
            ServiceOrder serviceOrder,
            DiagnosisIntakeSuggestionDto intakeSuggestion,
            string? dispatchTag,
            bool bypassConflictGuard,
            bool autoAssign,
            bool customerConsentAccepted,
            string? customerConsentChannel,
            string? customerConsentNote,
            long? assignedTeamId,
            long? leadTechnicianId,
            long? assistantTechnicianId);

        Task<DiagnosisHandoverSnapshotResponseDto> CreateHandoverSnapshotAsync(
            long serviceOrderId,
            DiagnosisHandoverSnapshotCreateDto request);

        Task<DiagnosisHandoverSnapshotResponseDto?> GetLatestHandoverSnapshotAsync(long serviceOrderId);

        Task<DiagnosisHandoverSnapshotResponseDto> ApplyHandoverCustomerDecisionAsync(
            long serviceOrderId,
            long snapshotId,
            DiagnosisHandoverDecisionCreateDto request);

        Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(
            long? branchId,
            DateTime? startDate,
            DateTime? endDate);
    }
}
