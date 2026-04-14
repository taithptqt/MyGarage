using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class DiagnosisService
    {
        private const int DefaultIntakeBufferMinutes = 15;
        private const int MaxIntakeBufferMinutes = 120;
        private const int IntakeConflictYellowThresholdMinutes = 5;
        private const int IntakeConflictRedThresholdMinutes = 15;

        private sealed class DiagnosisPackageProfile
        {
            public long StandardServiceId { get; init; }
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int NominalMinutes { get; init; }
            public int EffectiveMinutes { get; init; }
            public bool RequiresCustomerConsent { get; init; }
            public bool RequiresBayBlock { get; init; }
            public int Rank { get; init; }
        }

        private sealed class DiagnosisPackageCandidate
        {
            public long Id { get; init; }
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int StandardMinutes { get; init; }
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

        private const string LegacyMiniPackageCode = "MINI_10";
        private const string LegacyQuickPackageCode = "QUICK_30";
        private const string LegacyFullPackageCode = "FULL_60";

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
            var serviceOrder = await _context.ServiceOrders.AsNoTracking()
                .Where(x => x.Id == serviceOrderId)
                .Select(x => new
                {
                    x.Id,
                    x.ExecuteBranchId,
                    x.VehicleId,
                    x.OrderCategory,
                    x.CustomerNotes,
                    x.CheckInTime,
                    x.CreatedAt
                })
                .FirstOrDefaultAsync();

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
            var requestedServiceCount = await _context.ServiceOrderDetails.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId && !x.DiagnoseRecommendationId.HasValue)
                .CountAsync();

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
                throw new InvalidOperationException("Chua cau hinh goi chan doan trong StandardServices.");
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

            var suggestedTeam = await ResolveSuggestedTeamAsync(serviceOrder.ExecuteBranchId, normalizedDispatchTag);
            var effectiveDispatchTag = ResolveEffectiveDispatchTag(
                normalizedDispatchTag,
                suggestedTeam.specialtyTag,
                suggestedTeam.teamCode);
            DiagnosisDispatchSuggestionDto? dispatchSuggestion = null;
            if (suggestedTeam.teamId.HasValue)
            {
                try
                {
                    dispatchSuggestion = await BuildDispatchSuggestionAsync(serviceOrderId, suggestedTeam.teamId.Value);
                }
                catch
                {
                    // Keep intake suggestion functional even if dispatch suggestion cannot be built.
                }
            }

            if (finalPolicy.RequiresBayBlock && dispatchSuggestion?.SuggestedBayId == null && TryGetLowerPackage(packageProfiles, finalPolicy.Code, out var lowerByBay))
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
            if (finalPolicy.RequiresBayBlock && dispatchSuggestion?.SuggestedBayId == null)
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
                SuggestedBayId = dispatchSuggestion?.SuggestedBayId,
                SuggestedBayCode = dispatchSuggestion?.SuggestedBayCode,
                Reasons = reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
        }

        private async Task<string?> InferDispatchTagFromRequestedServicesAsync(long serviceOrderId)
        {
            var serviceCodes = await _context.ServiceOrderDetails.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId && x.StandardServiceId.HasValue && !x.DiagnoseRecommendationId.HasValue)
                .Join(
                    _context.StandardServices.AsNoTracking(),
                    detail => detail.StandardServiceId!.Value,
                    standard => standard.Id,
                    (detail, standard) => standard.Code)
                .ToListAsync();

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

        private async Task<IReadOnlyList<DiagnosisPackageProfile>> GetIntakePackageProfilesAsync()
        {
            var candidates = await _context.StandardServices.AsNoTracking()
                .Where(service => service.IsDeleted != true && service.IsActive != false)
                .Select(service => new DiagnosisPackageCandidate
                {
                    Id = service.Id,
                    Code = service.Code,
                    Name = service.Name,
                    StandardMinutes = service.StandardMinutes
                })
                .ToListAsync();
            var packageCandidates = candidates
                .Where(IsDiagnosisPackageCandidate)
                .OrderBy(service => Math.Max(0, service.StandardMinutes))
                .ThenBy(service => service.Id)
                .ToList();

            if (packageCandidates.Count == 0)
            {
                return Array.Empty<DiagnosisPackageProfile>();
            }

            var selectedCandidates = packageCandidates
                .DistinctBy(candidate => candidate.Id)
                .ToList();

            return selectedCandidates
                .Select((candidate, index) => BuildPackageProfile(candidate, index + 1, selectedCandidates.Count))
                .ToList();
        }

        private static DiagnosisPackageProfile BuildPackageProfile(
            DiagnosisPackageCandidate candidate,
            int rank,
            int totalPackages)
        {
            var nominalMinutes = Math.Max(0, candidate.StandardMinutes);
            var code = string.IsNullOrWhiteSpace(candidate.Code)
                ? $"INTAKE-PKG-{candidate.Id}"
                : candidate.Code.Trim().ToUpperInvariant();
            var name = string.IsNullOrWhiteSpace(candidate.Name)
                ? $"Goi chan doan #{candidate.Id}"
                : candidate.Name.Trim();
            var hasMultiplePackages = totalPackages > 1;
            var isBasePackage = hasMultiplePackages && rank == 1;
            return new DiagnosisPackageProfile
            {
                StandardServiceId = candidate.Id,
                Code = code,
                Name = name,
                NominalMinutes = nominalMinutes,
                EffectiveMinutes = isBasePackage ? 0 : nominalMinutes,
                RequiresCustomerConsent = hasMultiplePackages && rank > 1,
                RequiresBayBlock = nominalMinutes >= 45,
                Rank = rank
            };
        }

        private static bool IsDiagnosisPackageCandidate(DiagnosisPackageCandidate candidate)
        {
            return !string.IsNullOrWhiteSpace(candidate.Code)
                && candidate.Code.Trim().StartsWith("DG", StringComparison.OrdinalIgnoreCase);
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

            var lastVisitAt = await _context.ServiceOrders.AsNoTracking()
                .Where(x => x.VehicleId == vehicleId && x.Id != serviceOrderId)
                .Select(x => x.CheckInTime ?? x.CreatedAt)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .OrderByDescending(x => x)
                .FirstOrDefaultAsync();

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

            var hasRecentReworkOrWarranty = await _context.ServiceOrders.AsNoTracking()
                .Where(x => x.VehicleId == vehicleId && x.Id != serviceOrderId)
                .Where(x =>
                    x.OrderCategory == "REWORK" ||
                    x.OrderCategory == "WARRANTLY" ||
                    x.OrderCategory == "WARRANTY")
                .Where(x => (x.CheckInTime ?? x.CreatedAt) >= checkInTime.AddDays(-90))
                .AnyAsync();

            if (hasRecentReworkOrWarranty)
            {
                score += 10;
                reasons.Add("Xe có lịch sử warranty/rework gần đây.");
            }

            return Math.Max(0, score);
        }

        private async Task<int> EstimateMainServiceMinutesAsync(long serviceOrderId, string? orderCategory)
        {
            var serviceRows = await _context.ServiceOrderDetails.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.StandardServiceId.HasValue && !x.DiagnoseRecommendationId.HasValue)
                .Join(
                    _context.StandardServices.AsNoTracking(),
                    detail => detail.StandardServiceId!.Value,
                    standard => standard.Id,
                    (detail, standard) => new
                    {
                        standard.StandardMinutes,
                        detail.Quantity
                    })
                .ToListAsync();

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

        private static int EstimateFallbackMinutes(string? orderCategory)
        {
            var normalized = NormalizeOrderCategory(orderCategory);
            return normalized switch
            {
                "QUICK_SERVICE" => 20,
                "MAINTENANCE" => 45,
                "GENERAL_REPAIR" => 60,
                "INSURANCE_REPAIR" => 75,
                "REWORK" => 60,
                _ => 45
            };
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

            var nextBooking = await _context.Appointments.AsNoTracking()
                .Where(a =>
                    a.BranchId == branchId &&
                    a.StartTime >= checkInTime &&
                    !AppointmentStatusGroups.Closed.Contains(a.Status))
                .OrderBy(a => a.StartTime)
                .Select(a => new { a.Id, a.StartTime })
                .FirstOrDefaultAsync();

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

        private async Task<(long? teamId, string? teamName, string? specialtyTag, string? teamCode)> ResolveSuggestedTeamAsync(long branchId, string? normalizedDispatchTag)
        {
            var teamRows = await _context.Teams.AsNoTracking()
                .Where(x => x.BranchId == branchId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.SpecialtyTag,
                    x.Code
                })
                .ToListAsync();

            if (teamRows.Count == 0)
            {
                return (null, null, null, null);
            }

            var dispatchTags = SplitDispatchTags(normalizedDispatchTag);
            if (dispatchTags.Count > 0)
            {
                var matchByTag = teamRows
                    .Where(x =>
                    {
                        return dispatchTags.Any(tag => DispatchTagMatchesTeam(tag, x.SpecialtyTag, x.Code));
                    })
                    .OrderBy(x => x.Id)
                    .FirstOrDefault();

                if (matchByTag != null)
                {
                    return (matchByTag.Id, matchByTag.Name, matchByTag.SpecialtyTag, matchByTag.Code);
                }
            }

            var teamIds = teamRows.Select(x => x.Id).Distinct().ToList();
            var teamLoadRows = await _context.Diagnoses.AsNoTracking()
                .Where(x => x.AssignedTeamId.HasValue && teamIds.Contains(x.AssignedTeamId.Value))
                .Where(x =>
                    x.Status == DiagnosisStatusAssigned ||
                    x.Status == DiagnosisStatusPending)
                .GroupBy(x => x.AssignedTeamId!.Value)
                .Select(group => new
                {
                    TeamId = group.Key,
                    Load = group.Select(x => x.ServiceOrderId).Distinct().Count()
                })
                .ToListAsync();

            var loadMap = teamLoadRows.ToDictionary(x => x.TeamId, x => x.Load);
            var bestTeam = teamRows
                .OrderBy(x => loadMap.TryGetValue(x.Id, out var load) ? load : 0)
                .ThenBy(x => x.Id)
                .First();

            return (bestTeam.Id, bestTeam.Name, bestTeam.SpecialtyTag, bestTeam.Code);
        }

        private static string? ResolveEffectiveDispatchTag(string? normalizedDispatchTag, string? teamSpecialtyTag, string? teamCode)
        {
            if (!string.IsNullOrWhiteSpace(normalizedDispatchTag))
            {
                return normalizedDispatchTag;
            }

            var normalizedSpecialty = NormalizeDispatchTag(teamSpecialtyTag);
            if (!string.IsNullOrWhiteSpace(normalizedSpecialty))
            {
                return normalizedSpecialty;
            }

            var normalizedTeamCode = NormalizeToken(teamCode);
            if (string.IsNullOrWhiteSpace(normalizedTeamCode))
            {
                return null;
            }

            if (string.Equals(normalizedTeamCode, TeamCodeFastMaint, StringComparison.OrdinalIgnoreCase))
            {
                return "MAINTENANCE";
            }

            if (string.Equals(normalizedTeamCode, TeamCodeEngChas1, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedTeamCode, TeamCodeEngChas2, StringComparison.OrdinalIgnoreCase))
            {
                return "ENGINE_CHASSIS";
            }

            if (string.Equals(normalizedTeamCode, TeamCodeElecAc, StringComparison.OrdinalIgnoreCase))
            {
                return "ELECTRICAL_AC";
            }

            if (string.Equals(normalizedTeamCode, TeamCodeBodyCare, StringComparison.OrdinalIgnoreCase))
            {
                return "BODY_PAINT";
            }

            if (string.Equals(normalizedTeamCode, TeamCodeCarCare, StringComparison.OrdinalIgnoreCase))
            {
                return "CAR_CARE";
            }

            return null;
        }

        private static DiagnosisPackageProfile ResolveScoredPackage(
            IReadOnlyList<DiagnosisPackageProfile> packages,
            int score,
            bool isMinorQuickFlow,
            string? requestedPackageCode,
            List<string> reasons)
        {
            if (TryResolvePackage(packages, requestedPackageCode, out var requestedPackage))
            {
                reasons.Add($"Le tan chon goi {requestedPackage.Code}.");
                return requestedPackage;
            }

            if (isMinorQuickFlow)
            {
                return packages[0];
            }

            if (score >= 70)
            {
                return packages[Math.Max(0, packages.Count - 1)];
            }

            if (score >= 35)
            {
                return packages[ResolveQuickPackageIndex(packages.Count)];
            }

            return packages[0];
        }

        private static bool TryResolvePackage(
            IReadOnlyList<DiagnosisPackageProfile> packages,
            string? rawCode,
            out DiagnosisPackageProfile package)
        {
            var normalized = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
            package = packages[0];

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            var exactMatch = packages.FirstOrDefault(item => item.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                package = exactMatch;
                return true;
            }

            if (normalized == "MINI" || normalized == LegacyMiniPackageCode)
            {
                package = packages[0];
                return true;
            }

            if (normalized == "QUICK" || normalized == LegacyQuickPackageCode)
            {
                package = packages[ResolveQuickPackageIndex(packages.Count)];
                return true;
            }

            if (normalized == "FULL" || normalized == LegacyFullPackageCode)
            {
                package = packages[Math.Max(0, packages.Count - 1)];
                return true;
            }

            return false;
        }

        private static bool TryGetLowerPackage(
            IReadOnlyList<DiagnosisPackageProfile> packages,
            string currentCode,
            out DiagnosisPackageProfile lowerPackage)
        {
            var normalized = (currentCode ?? string.Empty).Trim().ToUpperInvariant();
            var currentIndex = packages
                .Select((package, index) => new { package, index })
                .FirstOrDefault(item => item.package.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                ?.index ?? -1;
            lowerPackage = packages[0];

            if (currentIndex <= 0)
            {
                return false;
            }

            lowerPackage = packages[currentIndex - 1];
            return true;
        }

        private static int ResolveQuickPackageIndex(int packageCount)
        {
            if (packageCount <= 1)
            {
                return 0;
            }

            return Math.Min(1, packageCount - 1);
        }
        private static bool IsMinorQuickFlow(string? orderCategory, int mainServiceMinutes, int requestedServiceCount)
        {
            var normalized = NormalizeOrderCategory(orderCategory);
            return normalized == "QUICK_SERVICE" && mainServiceMinutes <= 30 && requestedServiceCount <= 1;
        }

        private static int NormalizeBufferMinutes(int? bufferMinutesOverride)
        {
            if (!bufferMinutesOverride.HasValue)
            {
                return DefaultIntakeBufferMinutes;
            }

            return Math.Clamp(bufferMinutesOverride.Value, 0, MaxIntakeBufferMinutes);
        }

        private static string NormalizeOrderCategory(string? rawCategory)
        {
            return (rawCategory ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }
    }
}
