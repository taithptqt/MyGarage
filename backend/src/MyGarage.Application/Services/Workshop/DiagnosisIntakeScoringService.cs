using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using static MyGarage.Services.DiagnosisIntakeScoringHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    public sealed class DiagnosisIntakeScoringService : IDiagnosisIntakeScoringService
    {
        private readonly IDiagnosisStore _store;
        private readonly IDiagnosisRoutingService _routingService;

        public DiagnosisIntakeScoringService(IDiagnosisStore store, IDiagnosisRoutingService routingService)
        {
            _store = store;
            _routingService = routingService;
        }

        private sealed class AppointmentConflictProbe
        {
            public DateTime BaseFinishAt { get; init; }
            public DateTime PredictedFinishAt { get; init; }
            public long? NextBookingId { get; init; }
            public DateTime? NextBookingStartTime { get; init; }
            public int ConflictMinutes { get; init; }
            public string ConflictLevel { get; init; } = "GREEN";
        }

        public async Task<IReadOnlyList<DiagnosisIntakePackageOptionDto>> GetIntakePackagesAsync()
        {
            var packages = await GetIntakePackageProfilesAsync();
            return packages
                .Select(package => new DiagnosisIntakePackageOptionDto
                {
                    StandardServiceId = package.StandardServiceId,
                    Code = package.Code,
                    Name = package.Name,
                    NominalMinutes = package.NominalMinutes,
                    EffectiveMinutes = package.EffectiveMinutes,
                    RequiresCustomerConsent = package.RequiresCustomerConsent,
                    RequiresBayBlock = package.RequiresBayBlock,
                    Rank = package.Rank
                })
                .ToList();
        }

        public async Task<DiagnosisIntakeSuggestionDto> BuildIntakeSuggestionAsync(
            long serviceOrderId,
            string? dispatchTag,
            int? bufferMinutesOverride = null,
            string? requestedPackageCode = null)
        {
            var serviceOrder = await _store.GetIntakeServiceOrderInfoAsync(serviceOrderId);

            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
            }

            var normalizedDispatchTag = NormalizeDispatchTag(dispatchTag);
            if (normalizedDispatchTag == null)
            {
                normalizedDispatchTag = await InferDispatchTagFromRequestedServicesAsync(serviceOrderId);
            }
            var checkInTime = serviceOrder.CheckInTime ?? serviceOrder.CreatedAt ?? VietnamTime.Now;
            var bufferMinutes = NormalizeBufferMinutes(bufferMinutesOverride);
            var mainServiceMinutes = await EstimateMainServiceMinutesAsync(serviceOrderId, serviceOrder.OrderCategory);
            var requestedServiceCount = await _store.CountServiceOrderDetailsAsync(serviceOrderId);

            var reasons = new List<string>();
            var riskScore = await ComputeIntakeRiskScoreAsync(
                serviceOrderId,
                serviceOrder.VehicleId,
                checkInTime,
                serviceOrder.OrderCategory,
                serviceOrder.CustomerNotes,
                normalizedDispatchTag,
                mainServiceMinutes,
                requestedServiceCount,
                reasons);

            var isMinorQuickFlow = IsMinorQuickFlow(serviceOrder.OrderCategory, mainServiceMinutes, requestedServiceCount);
            if (isMinorQuickFlow)
            {
                reasons.Add("Xe thuộc nhóm quick service nhẹ, ưu tiên gói intake mức cơ bản.");
            }

            var packageProfiles = await GetIntakePackageProfilesAsync();
            if (packageProfiles.Count == 0)
            {
                throw new InvalidOperationException("Chưa cấu hình gói chẩn đoán trong StandardServices.");
            }

            var scoredPolicy = ResolveScoredPackage(packageProfiles, riskScore, isMinorQuickFlow, requestedPackageCode, reasons);
            var finalPolicy = scoredPolicy;
            var usedFallback = false;

            var probe = await EvaluateAppointmentConflictAsync(
                serviceOrder.ExecuteBranchId,
                checkInTime,
                mainServiceMinutes,
                bufferMinutes,
                finalPolicy.EffectiveMinutes);

            while (probe.ConflictLevel == "RED" && TryGetLowerPackage(packageProfiles, finalPolicy.Code, out var lowerPolicy))
            {
                reasons.Add($"Gói {finalPolicy.Code} gây xung đột lịch > {IntakeConflictRedThresholdMinutes} phút, hạ về {lowerPolicy.Code}.");
                finalPolicy = lowerPolicy;
                usedFallback = true;
                probe = await EvaluateAppointmentConflictAsync(
                    serviceOrder.ExecuteBranchId,
                    checkInTime,
                    mainServiceMinutes,
                    bufferMinutes,
                    finalPolicy.EffectiveMinutes);
            }

            var bayOwnershipSuggestion = await ResolveSuggestedBayByServiceAsync(
                serviceOrderId,
                serviceOrder.ExecuteBranchId,
                serviceOrder.OrderCategory);
            var mappedDispatchFromBay = ResolveDispatchTagFromBayType(
                bayOwnershipSuggestion.bayType,
                normalizedDispatchTag);
            var suggestedTeam = await ResolveSuggestedTeamAsync(
                serviceOrder.ExecuteBranchId,
                mappedDispatchFromBay ?? normalizedDispatchTag);
            var effectiveDispatchTag = ResolveEffectiveDispatchTag(
                mappedDispatchFromBay ?? normalizedDispatchTag,
                suggestedTeam.specialtyTag,
                suggestedTeam.teamCode);
            DiagnosisDispatchSuggestionDto? dispatchSuggestion = null;
            if (suggestedTeam.teamId.HasValue)
            {
                try
                {
                    dispatchSuggestion = await _routingService.BuildDispatchSuggestionAsync(serviceOrderId, suggestedTeam.teamId.Value);
                }
                catch
                {
                    // Keep intake suggestion functional even if dispatch suggestion cannot be built.
                }
            }

            var suggestedBayId = bayOwnershipSuggestion.bayId ?? dispatchSuggestion?.SuggestedBayId;
            var suggestedBayCode = bayOwnershipSuggestion.bayCode ?? dispatchSuggestion?.SuggestedBayCode;

            if (finalPolicy.RequiresBayBlock && suggestedBayId == null && TryGetLowerPackage(packageProfiles, finalPolicy.Code, out var lowerByBay))
            {
                reasons.Add($"Không còn cầu nâng trống cho gói {finalPolicy.Code}, hạ về {lowerByBay.Code}.");
                finalPolicy = lowerByBay;
                usedFallback = true;
                probe = await EvaluateAppointmentConflictAsync(
                    serviceOrder.ExecuteBranchId,
                    checkInTime,
                    mainServiceMinutes,
                    bufferMinutes,
                    finalPolicy.EffectiveMinutes);
            }

            var canApply = probe.ConflictLevel != "RED";
            if (finalPolicy.RequiresBayBlock && suggestedBayId == null)
            {
                canApply = false;
                reasons.Add("Không có cầu nâng phù hợp để block cho gói tổng quát.");
            }

            if (probe.NextBookingStartTime.HasValue)
            {
                reasons.Add($"Booking tiếp theo lúc {probe.NextBookingStartTime.Value:yyyy-MM-dd HH:mm}.");
            }
            else
            {
                reasons.Add("Không có booking phía sau trong khung giờ hiện tại.");
            }

            return new DiagnosisIntakeSuggestionDto
            {
                ServiceOrderId = serviceOrderId,
                DispatchTag = effectiveDispatchTag,
                CheckInTime = checkInTime,
                BufferMinutes = bufferMinutes,
                MainServiceMinutes = mainServiceMinutes,
                BaseFinishAt = probe.BaseFinishAt,
                PredictedFinishAt = probe.PredictedFinishAt,
                T1EstimatedHandover = probe.PredictedFinishAt,
                ScoredPackageCode = scoredPolicy.Code,
                ScoredPackageName = scoredPolicy.Name,
                FinalPackageCode = finalPolicy.Code,
                FinalPackageName = finalPolicy.Name,
                PackageNominalMinutes = finalPolicy.NominalMinutes,
                PackageEffectiveMinutes = finalPolicy.EffectiveMinutes,
                RequiresCustomerConsent = finalPolicy.RequiresCustomerConsent,
                RequiresBayBlock = finalPolicy.RequiresBayBlock,
                UsedConflictFallback = usedFallback,
                ConflictLevel = probe.ConflictLevel,
                ConflictMinutes = probe.ConflictMinutes,
                CanApply = canApply,
                NextBookingId = probe.NextBookingId,
                NextBookingStartTime = probe.NextBookingStartTime,
                SuggestedTeamId = suggestedTeam.teamId,
                SuggestedTeamName = suggestedTeam.teamName,
                SuggestedLeadTechnicianId = dispatchSuggestion?.SuggestedLeadTechnicianId,
                SuggestedLeadTechnicianName = dispatchSuggestion?.SuggestedLeadTechnicianName,
                SuggestedAssistantTechnicianId = dispatchSuggestion?.SuggestedAssistantTechnicianId,
                SuggestedAssistantTechnicianName = dispatchSuggestion?.SuggestedAssistantTechnicianName,
                SuggestedBayId = suggestedBayId,
                SuggestedBayCode = suggestedBayCode,
                Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        private async Task<IReadOnlyList<DiagnosisPackageProfile>> GetIntakePackageProfilesAsync()
        {
            var candidates = (await _store.GetActiveStandardServiceCandidatesAsync())
                .Select(service => new DiagnosisPackageCandidate
                {
                    Id = service.Id,
                    Code = service.Code ?? string.Empty,
                    Name = service.Name ?? string.Empty,
                    StandardMinutes = service.StandardMinutes
                })
                .ToList();

            return BuildPackageProfiles(candidates);
        }

        private async Task<int> ComputeIntakeRiskScoreAsync(
            long serviceOrderId,
            long vehicleId,
            DateTime checkInTime,
            string? orderCategory,
            string? customerNotes,
            string? normalizedDispatchTag,
            int mainServiceMinutes,
            int requestedServiceCount,
            List<string> reasons)
        {
            var score = 0;

            var lastVisitAt = await _store.GetLastVehicleVisitAtAsync(vehicleId, serviceOrderId) ?? default;

            if (lastVisitAt != default)
            {
                var daysSinceLastVisit = (int)Math.Floor((checkInTime - lastVisitAt).TotalDays);
                if (daysSinceLastVisit > 180)
                {
                    score += 35;
                    reasons.Add($"Xe quá {daysSinceLastVisit} ngày chưa quay lại.");
                }
            }

            var normalizedCategory = NormalizeOrderCategory(orderCategory);
            if (normalizedCategory == "GENERAL_REPAIR" || normalizedCategory == "INSURANCE_REPAIR" || normalizedCategory == "REWORK")
            {
                score += 20;
                reasons.Add("Lệnh thuộc nhóm sửa chữa trung bình/chuyên sâu.");
            }
            else if (normalizedCategory == "MAINTENANCE")
            {
                score += 10;
                reasons.Add("Lệnh bảo dưỡng có nguy cơ phát hiện lỗi phát sinh.");
            }

            if (mainServiceMinutes >= 90)
            {
                score += 15;
                reasons.Add("Tổng thời lượng dịch vụ chính cao.");
            }
            else if (mainServiceMinutes >= 45)
            {
                score += 8;
                reasons.Add("Tổng thời lượng dịch vụ chính ở mức trung bình.");
            }

            if (requestedServiceCount >= 3)
            {
                score += 10;
                reasons.Add("Khách đang yêu cầu nhiều hạng mục.");
            }

            var normalizedNotes = (customerNotes ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalizedNotes))
            {
                if (normalizedNotes.Contains("obd", StringComparison.OrdinalIgnoreCase) ||
                    normalizedNotes.Contains("check engine", StringComparison.OrdinalIgnoreCase) ||
                    normalizedNotes.Contains("ma loi", StringComparison.OrdinalIgnoreCase) ||
                    normalizedNotes.Contains("den bao", StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                    reasons.Add("Có dấu hiệu đèn báo/mã lỗi hệ thống.");
                }

                if (normalizedNotes.Contains("run", StringComparison.OrdinalIgnoreCase) ||
                    normalizedNotes.Contains("phanh", StringComparison.OrdinalIgnoreCase) ||
                    normalizedNotes.Contains("gam", StringComparison.OrdinalIgnoreCase) ||
                    normalizedNotes.Contains("dien", StringComparison.OrdinalIgnoreCase) ||
                    normalizedNotes.Contains("dieu hoa", StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                    reasons.Add("Mô tả triệu chứng liên quan hệ thống cần test nhanh.");
                }
            }

            if (!string.IsNullOrWhiteSpace(normalizedDispatchTag))
            {
                score += 6;
                reasons.Add($"Có chuyên môn điều phối {normalizedDispatchTag}.");
            }

            var hasRecentReworkOrWarranty = await _store.HasRecentReworkOrWarrantyAsync(
                vehicleId,
                serviceOrderId,
                checkInTime.AddDays(-90));

            if (hasRecentReworkOrWarranty)
            {
                score += 10;
                reasons.Add("Xe có lịch sử warranty/rework gần đây.");
            }

            return Math.Max(0, score);
        }

        private async Task<int> EstimateMainServiceMinutesAsync(long serviceOrderId, string? orderCategory)
        {
            var serviceRows = await _store.GetRequestedServiceDurationsAsync(serviceOrderId);

            if (serviceRows.Count == 0)
            {
                return EstimateFallbackMinutes(orderCategory);
            }

            var totalMinutes = serviceRows.Sum(x =>
            {
                var standardMinutes = Math.Max(0, x.StandardMinutes);
                var quantity = x.Quantity <= 0 ? 1m : x.Quantity;
                return standardMinutes * quantity;
            });

            if (totalMinutes <= 0)
            {
                return EstimateFallbackMinutes(orderCategory);
            }

            var rounded = (int)Math.Ceiling(totalMinutes);
            return Math.Clamp(rounded, 10, 600);
        }

        private async Task<AppointmentConflictProbe> EvaluateAppointmentConflictAsync(
            long branchId,
            DateTime checkInTime,
            int mainServiceMinutes,
            int bufferMinutes,
            int packageEffectiveMinutes)
        {
            var baseFinishAt = checkInTime.AddMinutes(Math.Max(0, mainServiceMinutes + bufferMinutes));
            var predictedFinishAt = baseFinishAt.AddMinutes(Math.Max(0, packageEffectiveMinutes));

            var nextBooking = await _store.GetNextOpenAppointmentAsync(branchId, checkInTime, AppointmentStatusGroups.Closed);

            var conflictMinutes = 0;
            if (nextBooking != null)
            {
                var overlap = predictedFinishAt - nextBooking.StartTime;
                if (overlap.TotalMinutes > 0)
                {
                    conflictMinutes = (int)Math.Ceiling(overlap.TotalMinutes);
                }
            }

            var conflictLevel = conflictMinutes > IntakeConflictRedThresholdMinutes
                ? "RED"
                : conflictMinutes > IntakeConflictYellowThresholdMinutes
                    ? "YELLOW"
                    : "GREEN";

            return new AppointmentConflictProbe
            {
                BaseFinishAt = baseFinishAt,
                PredictedFinishAt = predictedFinishAt,
                NextBookingId = nextBooking?.Id,
                NextBookingStartTime = nextBooking?.StartTime,
                ConflictMinutes = conflictMinutes,
                ConflictLevel = conflictLevel
            };
        }

        private async Task<(long? bayId, string? bayCode, string? bayType)> ResolveSuggestedBayByServiceAsync(
            long serviceOrderId,
            long branchId,
            string? orderCategory)
        {
            if (branchId <= 0) return (null, null, null);

            var serviceSignals = await _store.GetServiceSignalsForOrderAsync(serviceOrderId);

            var needsAlignment = serviceSignals.Any(x =>
                NormalizeToken(x.Code).Contains("ALIGN", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Code).Contains("SC_G04", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("CAN_CHINH_THUOC_LAI", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("DO_CHUM", StringComparison.OrdinalIgnoreCase));

            var needsElectrical = serviceSignals.Any(x =>
                NormalizeToken(x.Code).StartsWith("SC_D", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Code).Contains("ELEC", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("DIEN", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("MAY_LANH", StringComparison.OrdinalIgnoreCase));

            var needsDetailing = serviceSignals.Any(x =>
                NormalizeToken(x.Code).StartsWith("DT_", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("DETAIL", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("CERAMIC", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("PPF", StringComparison.OrdinalIgnoreCase) ||
                NormalizeToken(x.Name).Contains("CHAM_SOC", StringComparison.OrdinalIgnoreCase));

            var preferredBayTypes = new List<string>();
            var normalizedOrderCategory = NormalizeToken(orderCategory);
            switch (normalizedOrderCategory)
            {
                case "MAINTENANCE":
                case "QUICK_SERVICE":
                    preferredBayTypes.Add(BayTypeQsLift);
                    preferredBayTypes.Add(BayTypeQsFlat);
                    if (await _routingService.CanQuickTeamOverflowToGeneralRepairAsync(serviceOrderId, branchId))
                    {
                        preferredBayTypes.Add(BayTypeGrLift);
                    }
                    break;
                case "GENERAL_REPAIR":
                    if (needsAlignment) preferredBayTypes.Add(BayTypeGrAlign);
                    if (needsElectrical) preferredBayTypes.Add(BayTypeGrElectrical);
                    preferredBayTypes.Add(BayTypeGrLift);
                    break;
                case "INSURANCE_REPAIR":
                    preferredBayTypes.Add(BayTypeSpBodyPaint);
                    break;
                case "REWORK":
                case "WARRANTLY":
                case "WARRANTY":
                    if (needsAlignment) preferredBayTypes.Add(BayTypeGrAlign);
                    preferredBayTypes.Add(BayTypeGrLift);
                    if (needsElectrical) preferredBayTypes.Add(BayTypeGrElectrical);
                    break;
            }

            if (needsDetailing)
            {
                preferredBayTypes.Add(BayTypeSpDetailing);
            }

            preferredBayTypes.Add(BayTypeSpDiagnose);
            preferredBayTypes = preferredBayTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeToken)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (preferredBayTypes.Count == 0) return (null, null, null);

            var preferredBayTypeOrder = preferredBayTypes
                .Select((value, index) => new { value, index })
                .ToDictionary(x => x.value, x => x.index, StringComparer.OrdinalIgnoreCase);

            var bays = await _store.GetActiveBranchBaysAsync(branchId);

            var bay = bays
                .Where(x => !x.CurrentOccupiedVehicleId.HasValue && IsOperationalBayAvailable(x.OperationalStatus))
                .OrderBy(x =>
                {
                    var bayType = NormalizeToken(x.BayType);
                    return preferredBayTypeOrder.TryGetValue(bayType, out var order) ? order : int.MaxValue;
                })
                .ThenBy(x => x.Code ?? string.Empty)
                .FirstOrDefault();

            return bay == null
                ? (null, null, null)
                : (bay.Id, bay.Code, NormalizeToken(bay.BayType));
        }

        private async Task<string?> InferDispatchTagFromRequestedServicesAsync(long serviceOrderId)
        {
            var serviceCodes = await _store.GetRequestedServiceCodesAsync(serviceOrderId);

            if (serviceCodes.Count == 0)
            {
                return null;
            }

            var inferredDispatchTags = serviceCodes
                .Select(code =>
                {
                    var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
                    return DiagnosisCodeDispatchTagMap.TryGetValue(normalizedCode, out var tag)
                        ? NormalizeDispatchTag(tag)
                        : null;
                })
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .ToList();

            if (inferredDispatchTags.Count == 0)
            {
                return null;
            }

            var priorities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["ENGINE_CHASSIS"] = 1,
                ["ELECTRICAL_AC"] = 2,
                ["BODY_PAINT"] = 3,
                ["CAR_CARE"] = 4,
                ["MAINTENANCE"] = 5,
                ["QUICK_SERVICE"] = 6
            };

            var bestTag = inferredDispatchTags
                .GroupBy(x => x!, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    Tag = group.Key,
                    Count = group.Count(),
                    Priority = priorities.TryGetValue(group.Key, out var score) ? score : int.MaxValue
                })
                .OrderBy(x => x.Priority)
                .ThenByDescending(x => x.Count)
                .ThenBy(x => x.Tag)
                .Select(x => x.Tag)
                .FirstOrDefault();

            return NormalizeDispatchTag(bestTag);
        }

        private async Task<(long? teamId, string? teamName, string? specialtyTag, string? teamCode)> ResolveSuggestedTeamAsync(long branchId, string? normalizedDispatchTag)
        {
            var teamRows = await _store.GetTeamsByBranchNoTrackingAsync(branchId);

            if (teamRows.Count == 0)
            {
                return (null, null, null, null);
            }

            var dispatchTags = SplitDispatchTags(normalizedDispatchTag);
            if (dispatchTags.Count > 0)
            {
                var matchByTag = teamRows
                    .Where(x => dispatchTags.Any(tag => DispatchTagMatchesTeam(tag, x.SpecialtyTag, x.Code)))
                    .OrderBy(x => x.Id)
                    .FirstOrDefault();

                if (matchByTag != null)
                {
                    return (matchByTag.Id, matchByTag.Name, matchByTag.SpecialtyTag, matchByTag.Code);
                }
            }

            var teamIds = teamRows.Select(x => x.Id).Distinct().ToList();
            var rawLoadRows = await _store.GetTeamDiagnosisLoadRowsAsync(teamIds, DiagnosisStatusAssigned, DiagnosisStatusPending);

            var teamLoadRows = rawLoadRows
                .GroupBy(x => x.DiagnosisId)
                .Select(group => group
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.Id)
                    .First())
                .GroupBy(x => x.TeamId)
                .Select(group => new
                {
                    TeamId = group.Key,
                    Load = group.Select(x => x.ServiceOrderId).Distinct().Count()
                })
                .ToList();

            var loadMap = teamLoadRows.ToDictionary(x => x.TeamId, x => x.Load);
            var bestTeam = teamRows
                .OrderBy(x => loadMap.TryGetValue(x.Id, out var load) ? load : 0)
                .ThenBy(x => x.Id)
                .First();

            return (bestTeam.Id, bestTeam.Name, bestTeam.SpecialtyTag, bestTeam.Code);
        }
    }
}
