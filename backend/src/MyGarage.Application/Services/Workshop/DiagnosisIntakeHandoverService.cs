using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class DiagnosisIntakeHandoverService : IDiagnosisIntakeHandoverService
    {
        private readonly IDiagnosisIntakeScoringService _intakeScoringService;
        private readonly IDiagnosisHandoverSnapshotService _handoverSnapshotService;

        public DiagnosisIntakeHandoverService(
            IDiagnosisIntakeScoringService intakeScoringService,
            IDiagnosisHandoverSnapshotService handoverSnapshotService)
        {
            _intakeScoringService = intakeScoringService;
            _handoverSnapshotService = handoverSnapshotService;
        }

        public Task<IReadOnlyList<DiagnosisIntakePackageOptionDto>> GetIntakePackagesAsync()
        {
            return _intakeScoringService.GetIntakePackagesAsync();
        }

        public Task<DiagnosisIntakeSuggestionDto> BuildIntakeSuggestionAsync(
            long serviceOrderId,
            string? dispatchTag,
            int? bufferMinutesOverride = null,
            string? requestedPackageCode = null)
        {
            return _intakeScoringService.BuildIntakeSuggestionAsync(
                serviceOrderId,
                dispatchTag,
                bufferMinutesOverride,
                requestedPackageCode);
        }

        public Task RecordIntakeSnapshotAsync(
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
            long? assistantTechnicianId)
        {
            return _handoverSnapshotService.RecordIntakeSnapshotAsync(
                serviceOrder,
                intakeSuggestion,
                dispatchTag,
                bypassConflictGuard,
                autoAssign,
                customerConsentAccepted,
                customerConsentChannel,
                customerConsentNote,
                assignedTeamId,
                leadTechnicianId,
                assistantTechnicianId);
        }

        public Task<DiagnosisHandoverSnapshotResponseDto> CreateHandoverSnapshotAsync(
            long serviceOrderId,
            DiagnosisHandoverSnapshotCreateDto request)
        {
            return _handoverSnapshotService.CreateHandoverSnapshotAsync(serviceOrderId, request);
        }

        public Task<DiagnosisHandoverSnapshotResponseDto?> GetLatestHandoverSnapshotAsync(long serviceOrderId)
        {
            return _handoverSnapshotService.GetLatestHandoverSnapshotAsync(serviceOrderId);
        }

        public Task<DiagnosisHandoverSnapshotResponseDto> ApplyHandoverCustomerDecisionAsync(
            long serviceOrderId,
            long snapshotId,
            DiagnosisHandoverDecisionCreateDto request)
        {
            return _handoverSnapshotService.ApplyHandoverCustomerDecisionAsync(serviceOrderId, snapshotId, request);
        }

        public Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(
            long? branchId,
            DateTime? startDate,
            DateTime? endDate)
        {
            return _handoverSnapshotService.GetIntakeConversionKpiAsync(branchId, startDate, endDate);
        }
    }
}
