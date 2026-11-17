using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisIntakeScoringHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;
using static MyGarage.Services.DiagnosisSnapshotCodec;

namespace MyGarage.Services
{
    public sealed class DiagnosisHandoverSnapshotService : IDiagnosisHandoverSnapshotService
    {
        private readonly IDiagnosisStore _store;
        private readonly IDiagnosisIntakeKpiService _kpiService;

        public DiagnosisHandoverSnapshotService(
            IDiagnosisStore store,
            IDiagnosisIntakeKpiService kpiService)
        {
            _store = store;
            _kpiService = kpiService;
        }

        // Compatibility constructor for direct callers; the container uses the injected KPI service.
        public DiagnosisHandoverSnapshotService(IDiagnosisStore store, IDiagnosisIntakeScoringService intakeScoringService)
            : this(store, new DiagnosisIntakeKpiService(store, intakeScoringService))
        {
        }

        private const string ServiceOrderStatusWaitingApproval = nameof(ServiceOrderLifecycleStatus.WAITING_APPROVAL);
        private const string ServiceOrderStatusApproved = nameof(ServiceOrderLifecycleStatus.APPROVED);

        public async Task RecordIntakeSnapshotAsync(
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
            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa để ghi snapshot check-in.");
            }

            if (intakeSuggestion == null)
            {
                throw new InvalidOperationException("Không có gợi ý intake để ghi snapshot.");
            }

            var payload = new DiagnosisIntakeSnapshotPayload
            {
                Stage = SnapshotStageCheckInT1,
                ServiceOrderId = serviceOrder.Id,
                DispatchTag = NormalizeDispatchTag(dispatchTag),
                CheckInTime = intakeSuggestion.CheckInTime,
                BufferMinutes = intakeSuggestion.BufferMinutes,
                MainServiceMinutes = intakeSuggestion.MainServiceMinutes,
                PackageEffectiveMinutes = intakeSuggestion.PackageEffectiveMinutes,
                T1EstimatedHandover = intakeSuggestion.T1EstimatedHandover,
                IntakePackageCode = string.IsNullOrWhiteSpace(intakeSuggestion.FinalPackageCode)
                    ? string.Empty
                    : intakeSuggestion.FinalPackageCode.Trim().ToUpperInvariant(),
                IntakePackageName = string.IsNullOrWhiteSpace(intakeSuggestion.FinalPackageName)
                    ? (intakeSuggestion.FinalPackageCode ?? string.Empty)
                    : intakeSuggestion.FinalPackageName,
                RequiresCustomerConsent = intakeSuggestion.RequiresCustomerConsent,
                CustomerConsentAccepted = customerConsentAccepted,
                CustomerConsentChannel = NormalizeText(customerConsentChannel),
                CustomerConsentNote = NormalizeText(customerConsentNote),
                BypassConflictGuard = bypassConflictGuard,
                ConflictLevel = string.IsNullOrWhiteSpace(intakeSuggestion.ConflictLevel) ? "GREEN" : intakeSuggestion.ConflictLevel.Trim().ToUpperInvariant(),
                ConflictMinutes = Math.Max(0, intakeSuggestion.ConflictMinutes),
                AssignedTeamId = assignedTeamId ?? intakeSuggestion.SuggestedTeamId,
                LeadTechnicianId = leadTechnicianId.GetValueOrDefault() > 0
                    ? leadTechnicianId
                    : intakeSuggestion.SuggestedLeadTechnicianId,
                AssistantTechnicianId = assistantTechnicianId ?? intakeSuggestion.SuggestedAssistantTechnicianId,
                CreatedAt = VietnamTime.Now
            };

            var createdBy = serviceOrder.ReceptionistId ?? serviceOrder.ServiceAdvisorId;
            if (autoAssign && leadTechnicianId.GetValueOrDefault() > 0)
            {
                createdBy = leadTechnicianId;
            }

            await PersistSnapshotAsync(serviceOrder.Id, payload, createdBy);
        }

        public async Task<DiagnosisHandoverSnapshotResponseDto> CreateHandoverSnapshotAsync(long serviceOrderId, DiagnosisHandoverSnapshotCreateDto request)
        {
            var serviceOrder = await _store.GetServiceOrderByIdAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
            }

            var t1 = request.T1EstimatedHandoverOverride ?? await ResolveLatestT1Async(serviceOrderId, serviceOrder);
            var additionalRepairMinutes = Math.Clamp(request.AdditionalRepairMinutes, 0, 24 * 60);
            var t2 = t1.AddMinutes(additionalRepairMinutes);

            var payload = new DiagnosisHandoverSnapshotPayload
            {
                Stage = SnapshotStageAfterDiagnosisV1,
                ServiceOrderId = serviceOrderId,
                T1EstimatedHandover = t1,
                AdditionalRepairMinutes = additionalRepairMinutes,
                T2EstimatedHandover = t2,
                ApprovalState = ApprovalStatePending,
                CustomerApproved = null,
                DecisionChannel = null,
                DecisionNote = null,
                CustomerDecisionAt = null,
                DecidedByStaffId = null,
                QuoteChannel = NormalizeText(request.QuoteChannel),
                QuoteSummary = NormalizeText(request.QuoteSummary),
                QuoteAttachmentUrl = NormalizeText(request.QuoteAttachmentUrl),
                CreatedAt = VietnamTime.Now,
                CreatedByStaffId = request.CreatedByStaffId
            };

            var snapshot = await PersistSnapshotAsync(
                serviceOrderId,
                payload,
                request.CreatedByStaffId ?? serviceOrder.ReceptionistId ?? serviceOrder.ServiceAdvisorId);

            serviceOrder.Status = ServiceOrderStatusWaitingApproval;
            serviceOrder.UpdatedAt = VietnamTime.Now;
            _store.UpdateServiceOrder(serviceOrder);
            await _store.SaveChangesAsync();

            return ToHandoverResponse(snapshot, payload);
        }

        public async Task<DiagnosisHandoverSnapshotResponseDto?> GetLatestHandoverSnapshotAsync(long serviceOrderId)
        {
            var snapshots = await _store.GetSnapshotsByServiceOrderNoTrackingAsync(serviceOrderId);

            foreach (var snapshot in snapshots)
            {
                if (TryParseHandoverSnapshot(snapshot.SnapshotData, out var payload))
                {
                    return ToHandoverResponse(snapshot, payload!);
                }
            }

            return null;
        }

        public async Task<DiagnosisHandoverSnapshotResponseDto> ApplyHandoverCustomerDecisionAsync(
            long serviceOrderId,
            long snapshotId,
            DiagnosisHandoverDecisionCreateDto request)
        {
            var snapshot = await _store.GetSnapshotByIdAndOrderAsync(snapshotId, serviceOrderId);
            if (snapshot == null)
            {
                throw new InvalidOperationException("Không tìm thấy snapshot báo giá cần xử lý.");
            }

            if (!TryParseHandoverSnapshot(snapshot.SnapshotData, out var payload) || payload == null)
            {
                throw new InvalidOperationException("Snapshot không thuộc luồng T2 sau chẩn đoán.");
            }

            var latest = await GetLatestHandoverSnapshotAsync(serviceOrderId);
            if (latest == null || latest.SnapshotId != snapshotId)
                throw new InvalidOperationException("Bản bàn giao không còn là phiên bản mới nhất.");
            if (payload.CustomerApproved.HasValue)
                throw new InvalidOperationException("Bản bàn giao đã có quyết định của khách hàng.");

            var serviceOrder = await _store.GetServiceOrderByIdAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
            }

            var now = VietnamTime.Now;
            payload.CustomerApproved = request.Approved;
            payload.ApprovalState = request.Approved ? ApprovalStateApproved : ApprovalStateRejected;
            payload.DecisionChannel = NormalizeText(request.DecisionChannel);
            payload.DecisionNote = NormalizeText(request.DecisionNote);
            payload.CustomerDecisionAt = now;
            payload.DecidedByStaffId = request.DecidedByStaffId;

            snapshot.SnapshotData = JsonSerializer.Serialize(payload, SnapshotWriteOptions);
            _store.UpdateServiceOrderSnapshot(snapshot);

            if (request.Approved)
            {
                serviceOrder.EstimatedHandover = payload.T2EstimatedHandover;
                serviceOrder.CustomerApprovedAt = now;
                serviceOrder.Status = ServiceOrderStatusApproved;
            }
            else
            {
                serviceOrder.Status = ServiceOrderStatusDiagnosing;
            }

            serviceOrder.UpdatedAt = now;
            _store.UpdateServiceOrder(serviceOrder);
            await _store.SaveChangesAsync();

            return ToHandoverResponse(snapshot, payload);
        }

        public Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(long? branchId, DateTime? startDate, DateTime? endDate)
            => _kpiService.GetIntakeConversionKpiAsync(branchId, startDate, endDate);

        private async Task<DateTime> ResolveLatestT1Async(long serviceOrderId, ServiceOrder serviceOrder)
        {
            var snapshots = await _store.GetSnapshotDataListByServiceOrderNoTrackingAsync(serviceOrderId);

            foreach (var snapshotData in snapshots)
            {
                if (TryParseIntakeSnapshot(snapshotData, out var intakePayload) && intakePayload != null)
                {
                    return intakePayload.T1EstimatedHandover;
                }

                if (TryParseHandoverSnapshot(snapshotData, out var handoverPayload) && handoverPayload != null)
                {
                    return handoverPayload.T1EstimatedHandover;
                }
            }

            return serviceOrder.EstimatedHandover
                ?? serviceOrder.PromisedHandoverTime
                ?? serviceOrder.CheckInTime
                ?? serviceOrder.CreatedAt
                ?? VietnamTime.Now;
        }

        private async Task<ServiceOrderSnapshot> PersistSnapshotAsync(long serviceOrderId, object payload, long? createdBy)
        {
            var maxRevision = await _store.GetMaxSnapshotRevisionAsync(serviceOrderId);

            var snapshot = new ServiceOrderSnapshot
            {
                ServiceOrderId = serviceOrderId,
                RevisionNumber = maxRevision + 1,
                SnapshotData = JsonSerializer.Serialize(payload, SnapshotWriteOptions),
                CreatedBy = createdBy,
                CreatedAt = VietnamTime.Now
            };

            _store.AddServiceOrderSnapshot(snapshot);
            await _store.SaveChangesAsync();
            return snapshot;
        }

        private static DiagnosisHandoverSnapshotResponseDto ToHandoverResponse(
            ServiceOrderSnapshot snapshot,
            DiagnosisHandoverSnapshotPayload payload)
        {
            return new DiagnosisHandoverSnapshotResponseDto
            {
                SnapshotId = snapshot.Id,
                ServiceOrderId = snapshot.ServiceOrderId,
                RevisionNumber = snapshot.RevisionNumber,
                Stage = payload.Stage,
                T1EstimatedHandover = payload.T1EstimatedHandover,
                AdditionalRepairMinutes = payload.AdditionalRepairMinutes,
                T2EstimatedHandover = payload.T2EstimatedHandover,
                ApprovalState = payload.ApprovalState,
                CustomerApproved = payload.CustomerApproved,
                DecisionChannel = payload.DecisionChannel,
                DecisionNote = payload.DecisionNote,
                CustomerDecisionAt = payload.CustomerDecisionAt,
                QuoteChannel = payload.QuoteChannel,
                QuoteSummary = payload.QuoteSummary,
                QuoteAttachmentUrl = payload.QuoteAttachmentUrl,
                CreatedAt = snapshot.CreatedAt ?? payload.CreatedAt,
                CreatedBy = snapshot.CreatedBy
            };
        }

        private static string? NormalizeText(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

    }
}
