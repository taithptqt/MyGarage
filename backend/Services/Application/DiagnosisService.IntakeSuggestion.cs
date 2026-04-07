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
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int NominalMinutes { get; init; }
            public int EffectiveMinutes { get; init; }
            public bool RequiresCustomerConsent { get; init; }
            public bool RequiresBayBlock { get; init; }
            public int Rank { get; init; }
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

        private static readonly DiagnosisPackageProfile MiniPackage = new()
        {
            Code = "MINI_10",
            Name = "Mini Check (10p)",
            NominalMinutes = 10,
            EffectiveMinutes = 0,
            RequiresCustomerConsent = false,
            RequiresBayBlock = false,
            Rank = 1
        };

        private static readonly DiagnosisPackageProfile QuickPackage = new()
        {
            Code = "QUICK_30",
            Name = "Kiểm tra nhanh (30p)",
            NominalMinutes = 30,
            EffectiveMinutes = 30,
            RequiresCustomerConsent = true,
            RequiresBayBlock = false,
            Rank = 2
        };

        private static readonly DiagnosisPackageProfile FullPackage = new()
        {
            Code = "FULL_60",
            Name = "Kiểm tra tổng quát (60p)",
            NominalMinutes = 60,
            EffectiveMinutes = 60,
            RequiresCustomerConsent = true,
            RequiresBayBlock = true,
            Rank = 3
        };

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
            var checkInTime = serviceOrder.CheckInTime ?? serviceOrder.CreatedAt ?? DateTime.UtcNow;
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
                reasons.Add("Xe thuộc nhóm quick service nhẹ, ưu tiên Mini Check lồng ghép.");
            }

            var scoredPolicy = ResolveScoredPackage(riskScore, isMinorQuickFlow, requestedPackageCode, reasons);
            var finalPolicy = scoredPolicy;
            var usedFallback = false;

            var probe = await EvaluateAppointmentConflictAsync(
                serviceOrder.ExecuteBranchId,
                checkInTime,
                mainServiceMinutes,
                bufferMinutes,
                finalPolicy.EffectiveMinutes);

            while (probe.ConflictLevel == "RED" && TryGetLowerPackage(finalPolicy.Code, out var lowerPolicy))
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

            if (finalPolicy.RequiresBayBlock && dispatchSuggestion?.SuggestedBayId == null && TryGetLowerPackage(finalPolicy.Code, out var lowerByBay))
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
                DispatchTag = normalizedDispatchTag,
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

        private async Task<(long? teamId, string? teamName)> ResolveSuggestedTeamAsync(long branchId, string? normalizedDispatchTag)
        {
            var teamRows = await _context.Teams.AsNoTracking()
                .Where(x => x.BranchId == branchId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.SpecialtyTag
                })
                .ToListAsync();

            if (teamRows.Count == 0)
            {
                return (null, null);
            }

            var dispatchTags = SplitDispatchTags(normalizedDispatchTag);
            if (dispatchTags.Count > 0)
            {
                var matchByTag = teamRows
                    .Where(x =>
                    {
                        var specialty = NormalizeDispatchTag(x.SpecialtyTag);
                        return specialty != null && dispatchTags.Contains(specialty, StringComparer.OrdinalIgnoreCase);
                    })
                    .OrderBy(x => x.Id)
                    .FirstOrDefault();

                if (matchByTag != null)
                {
                    return (matchByTag.Id, matchByTag.Name);
                }
            }

            var teamIds = teamRows.Select(x => x.Id).Distinct().ToList();
            var teamLoadRows = await _context.Diagnoses.AsNoTracking()
                .Where(x => x.AssignedTeamId.HasValue && teamIds.Contains(x.AssignedTeamId.Value))
                .Where(x =>
                    x.Status == DiagnosisStatusAssigned ||
                    x.Status == DiagnosisStatusPending ||
                    x.Status == DiagnosisStatusDraft)
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

            return (bestTeam.Id, bestTeam.Name);
        }

        private static DiagnosisPackageProfile ResolveScoredPackage(
            int score,
            bool isMinorQuickFlow,
            string? requestedPackageCode,
            List<string> reasons)
        {
            if (TryResolvePackage(requestedPackageCode, out var requestedPackage))
            {
                reasons.Add($"Lễ tân chọn gói {requestedPackage.Code}.");
                return requestedPackage;
            }

            if (isMinorQuickFlow)
            {
                return MiniPackage;
            }

            if (score >= 70)
            {
                return FullPackage;
            }

            if (score >= 35)
            {
                return QuickPackage;
            }

            return MiniPackage;
        }

        private static bool TryResolvePackage(string? rawCode, out DiagnosisPackageProfile package)
        {
            var normalized = (rawCode ?? string.Empty).Trim().ToUpperInvariant();
            package = MiniPackage;

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (normalized == "MINI" || normalized == "MINI_10")
            {
                package = MiniPackage;
                return true;
            }

            if (normalized == "QUICK" || normalized == "QUICK_30")
            {
                package = QuickPackage;
                return true;
            }

            if (normalized == "FULL" || normalized == "FULL_60")
            {
                package = FullPackage;
                return true;
            }

            return false;
        }

        private static bool TryGetLowerPackage(string currentCode, out DiagnosisPackageProfile lowerPackage)
        {
            var normalized = (currentCode ?? string.Empty).Trim().ToUpperInvariant();
            lowerPackage = MiniPackage;

            if (normalized == FullPackage.Code)
            {
                lowerPackage = QuickPackage;
                return true;
            }

            if (normalized == QuickPackage.Code)
            {
                lowerPackage = MiniPackage;
                return true;
            }

            return false;
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
