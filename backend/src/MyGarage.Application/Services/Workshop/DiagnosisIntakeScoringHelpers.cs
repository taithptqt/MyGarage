using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.DTOs.Responses;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    internal static class DiagnosisIntakeScoringHelpers
    {
        internal const int DefaultIntakeBufferMinutes = 15;
        internal const int MaxIntakeBufferMinutes = 120;
        internal const int IntakeConflictYellowThresholdMinutes = 5;
        internal const int IntakeConflictRedThresholdMinutes = 15;

        internal const string LegacyMiniPackageCode = "MINI_10";
        internal const string LegacyQuickPackageCode = "QUICK_30";
        internal const string LegacyFullPackageCode = "FULL_60";

        internal sealed class DiagnosisPackageProfile
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

        internal sealed class DiagnosisPackageCandidate
        {
            public long Id { get; init; }
            public string Code { get; init; } = string.Empty;
            public string Name { get; init; } = string.Empty;
            public int StandardMinutes { get; init; }
        }

        internal static IReadOnlyList<DiagnosisPackageProfile> BuildPackageProfiles(IEnumerable<DiagnosisPackageCandidate> candidates)
        {
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

        internal static DiagnosisPackageProfile BuildPackageProfile(
            DiagnosisPackageCandidate candidate,
            int rank,
            int totalPackages)
        {
            var nominalMinutes = Math.Max(0, candidate.StandardMinutes);
            var code = string.IsNullOrWhiteSpace(candidate.Code)
                ? $"INTAKE-PKG-{candidate.Id}"
                : candidate.Code.Trim().ToUpperInvariant();
            var name = string.IsNullOrWhiteSpace(candidate.Name)
                ? $"Gói chẩn đoán #{candidate.Id}"
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

        internal static bool IsDiagnosisPackageCandidate(DiagnosisPackageCandidate candidate)
        {
            return !string.IsNullOrWhiteSpace(candidate.Code)
                && candidate.Code.Trim().StartsWith("DG", StringComparison.OrdinalIgnoreCase);
        }

        internal static DiagnosisPackageProfile ResolveScoredPackage(
            IReadOnlyList<DiagnosisPackageProfile> packages,
            int score,
            bool isMinorQuickFlow,
            string? requestedPackageCode,
            List<string> reasons)
        {
            if (TryResolvePackage(packages, requestedPackageCode, out var requestedPackage))
            {
                reasons.Add($"Lễ tân chọn gói {requestedPackage.Code}.");
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

        internal static bool TryResolvePackage(
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

        internal static bool TryGetLowerPackage(
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

        internal static int ResolveQuickPackageIndex(int packageCount)
        {
            if (packageCount <= 1)
            {
                return 0;
            }

            return Math.Min(1, packageCount - 1);
        }

        internal static bool IsMinorQuickFlow(string? orderCategory, int mainServiceMinutes, int requestedServiceCount)
        {
            var normalized = NormalizeOrderCategory(orderCategory);
            return normalized == "QUICK_SERVICE" && mainServiceMinutes <= 30 && requestedServiceCount <= 1;
        }

        internal static int NormalizeBufferMinutes(int? bufferMinutesOverride)
        {
            if (!bufferMinutesOverride.HasValue)
            {
                return DefaultIntakeBufferMinutes;
            }

            return Math.Clamp(bufferMinutesOverride.Value, 0, MaxIntakeBufferMinutes);
        }

        internal static string NormalizeOrderCategory(string? rawCategory)
        {
            return (rawCategory ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }

        internal static int EstimateFallbackMinutes(string? orderCategory)
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

        internal static string? ResolveDispatchTagFromBayType(string? bayType, string? fallbackDispatchTag)
        {
            var normalizedBayType = NormalizeToken(bayType);
            if (string.IsNullOrWhiteSpace(normalizedBayType))
            {
                return NormalizeDispatchTag(fallbackDispatchTag);
            }

            if (string.Equals(normalizedBayType, BayTypeGrAlign, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedBayType, BayTypeGrLift, StringComparison.OrdinalIgnoreCase))
            {
                return "ENGINE_CHASSIS";
            }

            if (string.Equals(normalizedBayType, BayTypeGrElectrical, StringComparison.OrdinalIgnoreCase))
            {
                return "ELECTRICAL_AC";
            }

            if (string.Equals(normalizedBayType, BayTypeSpBodyPaint, StringComparison.OrdinalIgnoreCase))
            {
                return "BODY_PAINT";
            }

            if (string.Equals(normalizedBayType, BayTypeSpDetailing, StringComparison.OrdinalIgnoreCase))
            {
                return "CAR_CARE";
            }

            if (string.Equals(normalizedBayType, BayTypeQsLift, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedBayType, BayTypeQsFlat, StringComparison.OrdinalIgnoreCase))
            {
                return "MAINTENANCE";
            }

            if (string.Equals(normalizedBayType, BayTypeSpDiagnose, StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeDispatchTag(fallbackDispatchTag) ?? "ENGINE_CHASSIS";
            }

            return NormalizeDispatchTag(fallbackDispatchTag);
        }

        internal static string? ResolveEffectiveDispatchTag(string? normalizedDispatchTag, string? teamSpecialtyTag, string? teamCode)
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

        internal static int ResolvePackageRank(string? packageCode, IReadOnlyList<DiagnosisIntakePackageOptionDto> packages)
        {
            var normalized = NormalizePackageCode(packageCode, packages);
            var matched = packages.FirstOrDefault(package => package.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return matched?.Rank ?? int.MaxValue;
        }

        internal static string NormalizePackageCode(string? packageCode, IReadOnlyList<DiagnosisIntakePackageOptionDto> packages)
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

        internal static string ResolvePackageName(string? packageCode, IReadOnlyList<DiagnosisIntakePackageOptionDto> packages)
        {
            var normalized = NormalizePackageCode(packageCode, packages);
            var matched = packages.FirstOrDefault(package => package.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            return matched?.Name ?? normalized;
        }
    }
}
