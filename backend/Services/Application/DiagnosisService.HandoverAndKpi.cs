using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class DiagnosisService
    {
        private const string SnapshotStageCheckInT1 = "CHECK_IN_T1";
        private const string SnapshotStageAfterDiagnosisV1 = "AFTER_DIAGNOSIS_V1";

        private const string ApprovalStatePending = "PENDING";
        private const string ApprovalStateApproved = "APPROVED";
        private const string ApprovalStateRejected = "REJECTED";

        private const string ServiceOrderStatusWaitingApproval = nameof(ServiceOrderLifecycleStatus.WAITING_APPROVAL);
        private const string ServiceOrderStatusApproved = nameof(ServiceOrderLifecycleStatus.APPROVED);

        private static readonly JsonSerializerOptions SnapshotReadOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions SnapshotWriteOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private sealed class DiagnosisIntakeSnapshotPayload
        {
            public string Stage { get; set; } = SnapshotStageCheckInT1;
            public long ServiceOrderId { get; set; }
            public string? DispatchTag { get; set; }
            public DateTime CheckInTime { get; set; }
            public int BufferMinutes { get; set; }
            public int MainServiceMinutes { get; set; }
            public int PackageEffectiveMinutes { get; set; }
            public DateTime T1EstimatedHandover { get; set; }
            public string IntakePackageCode { get; set; } = string.Empty;
            public string IntakePackageName { get; set; } = string.Empty;
            public bool RequiresCustomerConsent { get; set; }
            public bool CustomerConsentAccepted { get; set; }
            public string? CustomerConsentChannel { get; set; }
            public string? CustomerConsentNote { get; set; }
            public bool BypassConflictGuard { get; set; }
            public string ConflictLevel { get; set; } = "GREEN";
            public int ConflictMinutes { get; set; }
            public long? AssignedTeamId { get; set; }
            public long? LeadTechnicianId { get; set; }
            public long? AssistantTechnicianId { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private sealed class DiagnosisHandoverSnapshotPayload
        {
            public string Stage { get; set; } = SnapshotStageAfterDiagnosisV1;
            public long ServiceOrderId { get; set; }
            public DateTime T1EstimatedHandover { get; set; }
            public int AdditionalRepairMinutes { get; set; }
            public DateTime T2EstimatedHandover { get; set; }
            public string ApprovalState { get; set; } = ApprovalStatePending;
            public bool? CustomerApproved { get; set; }
            public string? DecisionChannel { get; set; }
            public string? DecisionNote { get; set; }
            public DateTime? CustomerDecisionAt { get; set; }
            public long? DecidedByStaffId { get; set; }
            public string? QuoteChannel { get; set; }
            public string? QuoteSummary { get; set; }
            public string? QuoteAttachmentUrl { get; set; }
            public DateTime CreatedAt { get; set; }
            public long? CreatedByStaffId { get; set; }
        }

        private sealed class IntakeKpiBucket
        {
            public int Total { get; set; }
            public int Approved { get; set; }
            public int Rejected { get; set; }
            public int Pending { get; set; }
        }

        public async Task RecordIntakeSnapshotAsync(
            ServiceOrder serviceOrder,
            DiagnosisIntakeSuggestionDto intakeSuggestion,
            string? dispatchTag,
            bool bypassConflictGuard,
            bool autoAssign,
            bool customerConsentAccepted,
            string? customerConsentChannel,
            string? customerConsentNote,
            IReadOnlyList<Diagnosis> diagnoses)
        {
            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Khong tim thay lenh sua chua de ghi snapshot check-in.");
            }

            if (intakeSuggestion == null)
            {
                throw new InvalidOperationException("Khong co goi y intake de ghi snapshot.");
            }

            var newestDiagnosis = (diagnoses ?? Array.Empty<Diagnosis>())
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                .FirstOrDefault();

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
                AssignedTeamId = newestDiagnosis != null ? GetAssignedTeamId(newestDiagnosis) ?? intakeSuggestion.SuggestedTeamId : intakeSuggestion.SuggestedTeamId,
                LeadTechnicianId = newestDiagnosis != null && GetTechnicianId(newestDiagnosis).GetValueOrDefault() > 0
                    ? GetTechnicianId(newestDiagnosis)
                    : intakeSuggestion.SuggestedLeadTechnicianId,
                AssistantTechnicianId = newestDiagnosis != null ? GetAssistantTechnicianId(newestDiagnosis) ?? intakeSuggestion.SuggestedAssistantTechnicianId : intakeSuggestion.SuggestedAssistantTechnicianId,
                CreatedAt = VietnamTime.Now
            };

            var createdBy = serviceOrder.ReceptionistId ?? serviceOrder.ServiceAdvisorId;
            if (autoAssign && newestDiagnosis != null && GetTechnicianId(newestDiagnosis).GetValueOrDefault() > 0)
            {
                createdBy = GetTechnicianId(newestDiagnosis);
            }

            await PersistSnapshotAsync(serviceOrder.Id, payload, createdBy);
        }

        public async Task<DiagnosisHandoverSnapshotResponseDto> CreateHandoverSnapshotAsync(long serviceOrderId, DiagnosisHandoverSnapshotCreateDto request)
        {
            var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Khong tim thay lenh sua chua.");
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
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();

            return ToHandoverResponse(snapshot, payload);
        }

        public async Task<DiagnosisHandoverSnapshotResponseDto?> GetLatestHandoverSnapshotAsync(long serviceOrderId)
        {
            var snapshots = await _context.ServiceOrderSnapshots.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .OrderByDescending(x => x.RevisionNumber)
                .ThenByDescending(x => x.CreatedAt)
                .ToListAsync();

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
            var snapshot = await _context.ServiceOrderSnapshots
                .FirstOrDefaultAsync(x => x.Id == snapshotId && x.ServiceOrderId == serviceOrderId);
            if (snapshot == null)
            {
                throw new InvalidOperationException("Khong tim thay snapshot bao gia can xu ly.");
            }

            if (!TryParseHandoverSnapshot(snapshot.SnapshotData, out var payload) || payload == null)
            {
                throw new InvalidOperationException("Snapshot khong thuoc luong T2 sau chan doan.");
            }

            var serviceOrder = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Khong tim thay lenh sua chua.");
            }

            var now = VietnamTime.Now;
            payload.CustomerApproved = request.Approved;
            payload.ApprovalState = request.Approved ? ApprovalStateApproved : ApprovalStateRejected;
            payload.DecisionChannel = NormalizeText(request.DecisionChannel);
            payload.DecisionNote = NormalizeText(request.DecisionNote);
            payload.CustomerDecisionAt = now;
            payload.DecidedByStaffId = request.DecidedByStaffId;

            snapshot.SnapshotData = JsonSerializer.Serialize(payload, SnapshotWriteOptions);
            _context.ServiceOrderSnapshots.Update(snapshot);

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
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();

            return ToHandoverResponse(snapshot, payload);
        }

        public async Task<DiagnosisIntakeKpiResponseDto> GetIntakeConversionKpiAsync(long? branchId, DateTime? startDate, DateTime? endDate)
        {
            var utcStart = (startDate ?? VietnamTime.Now.Date.AddDays(-30)).Date;
            var utcEndDate = (endDate ?? VietnamTime.Now.Date).Date;
            if (utcEndDate < utcStart)
            {
                (utcStart, utcEndDate) = (utcEndDate, utcStart);
            }

            var utcEnd = utcEndDate.AddDays(1).AddTicks(-1);

            var snapshotRows = await _context.ServiceOrderSnapshots.AsNoTracking()
                .Where(x => (x.CreatedAt ?? DateTime.MinValue) >= utcStart && (x.CreatedAt ?? DateTime.MinValue) <= utcEnd)
                .Select(x => new
                {
                    x.Id,
                    x.ServiceOrderId,
                    x.RevisionNumber,
                    x.CreatedAt,
                    x.SnapshotData
                })
                .ToListAsync();

            if (snapshotRows.Count == 0)
            {
                return new DiagnosisIntakeKpiResponseDto
                {
                    StartDate = utcStart,
                    EndDate = utcEnd,
                    BranchId = branchId
                };
            }

            var orderIds = snapshotRows.Select(x => x.ServiceOrderId).Distinct().ToList();
            var orderBranches = await _context.ServiceOrders.AsNoTracking()
                .Where(x => orderIds.Contains(x.Id))
                .Select(x => new { x.Id, x.ExecuteBranchId })
                .ToDictionaryAsync(x => x.Id, x => x.ExecuteBranchId);

            var scopedSnapshots = snapshotRows
                .Where(x => orderBranches.TryGetValue(x.ServiceOrderId, out var executeBranchId) && (!branchId.HasValue || executeBranchId == branchId.Value))
                .OrderByDescending(x => x.RevisionNumber)
                .ThenByDescending(x => x.CreatedAt ?? DateTime.MinValue)
                .ToList();

            var latestIntakeByOrder = new Dictionary<long, DiagnosisIntakeSnapshotPayload>();
            var latestHandoverByOrder = new Dictionary<long, DiagnosisHandoverSnapshotPayload>();

            foreach (var row in scopedSnapshots)
            {
                if (!latestIntakeByOrder.ContainsKey(row.ServiceOrderId) && TryParseIntakeSnapshot(row.SnapshotData, out var intakePayload))
                {
                    latestIntakeByOrder[row.ServiceOrderId] = intakePayload!;
                }

                if (!latestHandoverByOrder.ContainsKey(row.ServiceOrderId) && TryParseHandoverSnapshot(row.SnapshotData, out var handoverPayload))
                {
                    latestHandoverByOrder[row.ServiceOrderId] = handoverPayload!;
                }
            }

            var intakePackages = await GetIntakePackageProfilesAsync();
            var packageBuckets = intakePackages.ToDictionary(
                package => package.Code,
                _ => new IntakeKpiBucket(),
                StringComparer.OrdinalIgnoreCase);

            var teamBuckets = new Dictionary<long, IntakeKpiBucket>();

            foreach (var (serviceOrderId, intakePayload) in latestIntakeByOrder)
            {
                latestHandoverByOrder.TryGetValue(serviceOrderId, out var handoverPayload);
                var decisionState = ResolveDecisionState(handoverPayload);

                var packageCode = NormalizePackageCode(intakePayload.IntakePackageCode, intakePackages);
                if (!packageBuckets.TryGetValue(packageCode, out var packageBucket))
                {
                    packageBucket = new IntakeKpiBucket();
                    packageBuckets[packageCode] = packageBucket;
                }
                ApplyDecisionState(packageBucket, decisionState);

                var teamKey = intakePayload.AssignedTeamId ?? 0;
                if (!teamBuckets.TryGetValue(teamKey, out var teamBucket))
                {
                    teamBucket = new IntakeKpiBucket();
                    teamBuckets[teamKey] = teamBucket;
                }
                ApplyDecisionState(teamBucket, decisionState);
            }

            var teamIds = teamBuckets.Keys.Where(x => x > 0).Distinct().ToList();
            var teamNameMap = teamIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Teams.AsNoTracking()
                    .Where(x => teamIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Name })
                    .ToDictionaryAsync(x => x.Id, x => x.Name ?? $"Tổ #{x.Id}");

            var packageConversions = packageBuckets
                .Select(pair => new DiagnosisIntakePackageKpiItemDto
                {
                    PackageCode = pair.Key,
                    PackageName = ResolvePackageName(pair.Key, intakePackages),
                    TotalOrders = pair.Value.Total,
                    ApprovedAdditionalRepairOrders = pair.Value.Approved,
                    RejectedAdditionalRepairOrders = pair.Value.Rejected,
                    PendingDecisionOrders = pair.Value.Pending,
                    ApprovalRatePercent = ComputePercent(pair.Value.Approved, pair.Value.Total)
                })
                .OrderBy(x => ResolvePackageRank(x.PackageCode, intakePackages))
                .ToList();

            var teamConversions = teamBuckets
                .Select(pair => new DiagnosisIntakeTeamKpiItemDto
                {
                    TeamId = pair.Key > 0 ? pair.Key : null,
                    TeamName = pair.Key > 0 && teamNameMap.TryGetValue(pair.Key, out var teamName)
                        ? teamName
                        : "Chưa phân tổ",
                    TotalOrders = pair.Value.Total,
                    ApprovedAdditionalRepairOrders = pair.Value.Approved,
                    RejectedAdditionalRepairOrders = pair.Value.Rejected,
                    PendingDecisionOrders = pair.Value.Pending,
                    ApprovalRatePercent = ComputePercent(pair.Value.Approved, pair.Value.Total)
                })
                .OrderByDescending(x => x.TotalOrders)
                .ThenBy(x => x.TeamName)
                .ToList();

            return new DiagnosisIntakeKpiResponseDto
            {
                StartDate = utcStart,
                EndDate = utcEnd,
                BranchId = branchId,
                TotalIntakeOrders = latestIntakeByOrder.Count,
                TotalApprovedAdditionalRepairOrders = packageBuckets.Values.Sum(x => x.Approved),
                PackageConversions = packageConversions,
                TeamConversions = teamConversions
            };
        }

        private async Task<DateTime> ResolveLatestT1Async(long serviceOrderId, ServiceOrder serviceOrder)
        {
            var snapshots = await _context.ServiceOrderSnapshots.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .OrderByDescending(x => x.RevisionNumber)
                .ThenByDescending(x => x.CreatedAt)
                .Select(x => x.SnapshotData)
                .ToListAsync();

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
            var maxRevision = await _context.ServiceOrderSnapshots.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Select(x => (int?)x.RevisionNumber)
                .MaxAsync() ?? 0;

            var snapshot = new ServiceOrderSnapshot
            {
                ServiceOrderId = serviceOrderId,
                RevisionNumber = maxRevision + 1,
                SnapshotData = JsonSerializer.Serialize(payload, SnapshotWriteOptions),
                CreatedBy = createdBy,
                CreatedAt = VietnamTime.Now
            };

            _context.ServiceOrderSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();
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

        private static bool TryParseIntakeSnapshot(string? snapshotData, out DiagnosisIntakeSnapshotPayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(snapshotData))
            {
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<DiagnosisIntakeSnapshotPayload>(snapshotData, SnapshotReadOptions);
                return payload != null &&
                    string.Equals(payload.Stage, SnapshotStageCheckInT1, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        private static bool TryParseHandoverSnapshot(string? snapshotData, out DiagnosisHandoverSnapshotPayload? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(snapshotData))
            {
                return false;
            }

            try
            {
                payload = JsonSerializer.Deserialize<DiagnosisHandoverSnapshotPayload>(snapshotData, SnapshotReadOptions);
                return payload != null &&
                    string.Equals(payload.Stage, SnapshotStageAfterDiagnosisV1, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                payload = null;
                return false;
            }
        }

        private static void ApplyDecisionState(IntakeKpiBucket bucket, string decisionState)
        {
            bucket.Total += 1;

            if (decisionState == ApprovalStateApproved)
            {
                bucket.Approved += 1;
                return;
            }

            if (decisionState == ApprovalStateRejected)
            {
                bucket.Rejected += 1;
                return;
            }

            bucket.Pending += 1;
        }

        private static string ResolveDecisionState(DiagnosisHandoverSnapshotPayload? payload)
        {
            if (payload == null)
            {
                return ApprovalStatePending;
            }

            if (payload.CustomerApproved == true)
            {
                return ApprovalStateApproved;
            }

            if (payload.CustomerApproved == false)
            {
                return ApprovalStateRejected;
            }

            var normalizedState = NormalizeText(payload.ApprovalState)?.ToUpperInvariant();
            return normalizedState switch
            {
                ApprovalStateApproved => ApprovalStateApproved,
                ApprovalStateRejected => ApprovalStateRejected,
                _ => ApprovalStatePending
            };
        }

        private static int ResolvePackageRank(string? packageCode, IReadOnlyList<DiagnosisPackageProfile> packages)
        {
            var normalized = NormalizePackageCode(packageCode, packages);
            var matched = packages.FirstOrDefault(package => package.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return matched?.Rank ?? int.MaxValue;
        }

        private static string NormalizePackageCode(string? packageCode, IReadOnlyList<DiagnosisPackageProfile> packages)
        {
            var normalized = (packageCode ?? string.Empty).Trim().ToUpperInvariant();

            if (packages.Count == 0)
            {
                return normalized;
            }

            if (packages.Any(package => package.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return normalized;
            }

            return normalized switch
            {
                "MINI" or LegacyMiniPackageCode => packages[0].Code,
                "QUICK" or LegacyQuickPackageCode => packages[Math.Min(1, packages.Count - 1)].Code,
                "FULL" or LegacyFullPackageCode => packages[Math.Max(0, packages.Count - 1)].Code,
                _ => string.IsNullOrWhiteSpace(normalized) ? packages[0].Code : normalized
            };
        }

        private static string ResolvePackageName(string? packageCode, IReadOnlyList<DiagnosisPackageProfile> packages)
        {
            var normalized = NormalizePackageCode(packageCode, packages);
            var matched = packages.FirstOrDefault(package => package.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return matched?.Name ?? normalized;
        }

        private static string? NormalizeText(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private static decimal ComputePercent(int numerator, int denominator)
        {
            if (denominator <= 0)
            {
                return 0m;
            }

            var ratio = (decimal)numerator / denominator * 100m;
            return Math.Round(ratio, 2, MidpointRounding.AwayFromZero);
        }
    }
}
