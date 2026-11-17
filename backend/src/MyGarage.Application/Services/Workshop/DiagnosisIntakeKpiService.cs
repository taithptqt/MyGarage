using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using static MyGarage.Services.DiagnosisIntakeScoringHelpers;
using static MyGarage.Services.DiagnosisSnapshotCodec;

namespace MyGarage.Services;

public sealed class DiagnosisIntakeKpiService : IDiagnosisIntakeKpiService
{
    private readonly IDiagnosisStore _store;
    private readonly IDiagnosisIntakeScoringService _intakeScoringService;

    public DiagnosisIntakeKpiService(IDiagnosisStore store, IDiagnosisIntakeScoringService intakeScoringService)
    {
        _store = store;
        _intakeScoringService = intakeScoringService;
    }

        private sealed class IntakeKpiBucket
        {
            public int Total { get; set; }
            public int Approved { get; set; }
            public int Rejected { get; set; }
            public int Pending { get; set; }
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

            var snapshotRows = await _store.GetSnapshotsInRangeNoTrackingAsync(utcStart, utcEnd);

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
            var orderBranches = await _store.GetServiceOrderExecuteBranchMapAsync(orderIds);

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

            var intakePackages = await _intakeScoringService.GetIntakePackagesAsync();
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
                : (await _store.GetTeamNameMapAsync(teamIds))
                    .ToDictionary(x => x.Key, x => string.IsNullOrWhiteSpace(x.Value) ? $"Tổ #{x.Key}" : x.Value);

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
