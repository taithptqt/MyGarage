using System;
using System.Collections.Generic;
using System.Linq;
using MyGarage.Enums;

namespace MyGarage.Services
{
    internal static class DiagnosisRoutingHelpers
    {
        internal const string StaffStatusActive = nameof(StaffStatusCode.ACTIVE);
        internal const string DiagnosisStatusAssigned = nameof(DiagnosisStatus.ASSIGNED);
        internal const string DiagnosisStatusPending = nameof(DiagnosisStatus.PENDING);
        internal const string ServiceOrderStatusDiagnosing = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);

        internal const int DiagnosisQueueDefaultMinutes = 30;
        internal const int DiagnosisQueueDefaultBufferMinutes = 15;
        internal const string AutoDiagnosisQueueSlotMarker = "[AUTO_DIAG_QUEUE]";

        internal const string TeamCodeFastMaint = "FAST_MAINT";
        internal const string TeamCodeEngChas1 = "ENG_CHAS_1";
        internal const string TeamCodeEngChas2 = "ENG_CHAS_2";
        internal const string TeamCodeElecAc = "ELEC_AC";
        internal const string TeamCodeBodyCare = "BODY_CARE";
        internal const string TeamCodeCarCare = "CAR_CARE";

        internal const string BayTypeQsLift = "QS_LIFT";
        internal const string BayTypeQsFlat = "QS_FLAT";
        internal const string BayTypeGrLift = "GR_LIFT";
        internal const string BayTypeGrAlign = "GR_ALIGN";
        internal const string BayTypeGrElectrical = "GR_ELECTRICAL";
        internal const string BayTypeSpDiagnose = "SP_DIAGNOSE";
        internal const string BayTypeSpBodyPaint = "SP_BODY_PAINT";
        internal const string BayTypeSpDetailing = "SP_DETAILING";

        internal static readonly string[] QuickOrderCategories =
        [
            "MAINTENANCE",
            "QUICK_SERVICE"
        ];

        internal static readonly string[] DiagnoseFirstStatuses =
        [
            nameof(ServiceOrderLifecycleStatus.NEW),
            nameof(ServiceOrderLifecycleStatus.CHECKED_IN),
            nameof(ServiceOrderLifecycleStatus.DIAGNOSING)
        ];

        internal static readonly IReadOnlyDictionary<string, string> DiagnosisCodeDispatchTagMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DG-MINI"] = "MAINTENANCE",
            ["DG-GEN"] = "MAINTENANCE",
            ["DG-SCAN"] = "ELECTRICAL_AC",
            ["DG-ENG"] = "ENGINE_CHASSIS",
            ["DG-ELEC"] = "ELECTRICAL_AC",
            ["DG-AC"] = "ELECTRICAL_AC",
            ["DG-NOISE"] = "ENGINE_CHASSIS"
        };

        internal static string NormalizeToken(string? rawValue)
        {
            return (rawValue ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }

        internal static string? NormalizeDispatchTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return null;
            }

            return rawTag.Trim().ToUpperInvariant();
        }

        internal static IReadOnlyList<string> SplitDispatchTags(string? rawTag)
        {
            var normalized = NormalizeDispatchTag(rawTag);
            if (normalized == null)
            {
                return Array.Empty<string>();
            }

            var split = normalized
                .Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (split.Count > 0)
            {
                return split;
            }

            return new[] { normalized };
        }

        internal static bool DispatchTagMatchesTeam(string dispatchTag, string? specialtyTag, string? teamCode)
        {
            if (DispatchTagMatchesSpecialty(dispatchTag, specialtyTag ?? string.Empty))
            {
                return true;
            }

            var dispatch = NormalizeDispatchTag(dispatchTag);
            var code = NormalizeToken(teamCode);
            if (dispatch == null || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            if (dispatch == "ENGINE_CHASSIS")
            {
                return string.Equals(code, TeamCodeEngChas1, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(code, TeamCodeEngChas2, StringComparison.OrdinalIgnoreCase);
            }

            if (dispatch == "QUICK_SERVICE" || dispatch == "MAINTENANCE")
            {
                return string.Equals(code, TeamCodeFastMaint, StringComparison.OrdinalIgnoreCase);
            }

            if (dispatch == "ELECTRICAL_AC")
            {
                return string.Equals(code, TeamCodeElecAc, StringComparison.OrdinalIgnoreCase);
            }

            if (dispatch == "BODY_PAINT" || dispatch == "INSURANCE_REPAIR")
            {
                return string.Equals(code, TeamCodeBodyCare, StringComparison.OrdinalIgnoreCase);
            }

            if (dispatch == "CAR_CARE" || dispatch == "DETAILING")
            {
                return string.Equals(code, TeamCodeCarCare, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        internal static bool DispatchTagMatchesSpecialty(string dispatchTag, string specialtyTag)
        {
            var dispatch = NormalizeDispatchTag(dispatchTag);
            var specialty = NormalizeDispatchTag(specialtyTag);
            if (dispatch == null)
            {
                return true;
            }
            if (specialty == null)
            {
                return false;
            }

            if (string.Equals(dispatch, specialty, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if ((dispatch == "QUICK_SERVICE" && specialty == "MAINTENANCE") ||
                (dispatch == "MAINTENANCE" && specialty == "QUICK_SERVICE"))
            {
                return true;
            }

            if ((dispatch == "INSURANCE_REPAIR" && specialty == "BODY_PAINT") ||
                (dispatch == "BODY_PAINT" && specialty == "INSURANCE_REPAIR"))
            {
                return true;
            }

            if ((dispatch == "DETAILING" && specialty == "CAR_CARE") ||
                (dispatch == "CAR_CARE" && specialty == "DETAILING") ||
                (dispatch == "BODY_PAINT" && specialty == "CAR_CARE") ||
                (dispatch == "CAR_CARE" && specialty == "BODY_PAINT"))
            {
                return true;
            }

            return false;
        }

        internal static IReadOnlyList<string> ResolveBaseBayTypesForTeam(string? teamCode, string? specialtyTag)
        {
            var code = NormalizeToken(teamCode);
            var specialty = NormalizeDispatchTag(specialtyTag);

            if (string.Equals(code, TeamCodeFastMaint, StringComparison.OrdinalIgnoreCase) ||
                specialty == "MAINTENANCE" ||
                specialty == "QUICK_SERVICE")
            {
                return [BayTypeQsLift, BayTypeQsFlat];
            }

            if (string.Equals(code, TeamCodeEngChas1, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, TeamCodeEngChas2, StringComparison.OrdinalIgnoreCase) ||
                specialty == "ENGINE_CHASSIS")
            {
                return [BayTypeGrLift, BayTypeGrAlign];
            }

            if (string.Equals(code, TeamCodeElecAc, StringComparison.OrdinalIgnoreCase) || specialty == "ELECTRICAL_AC")
            {
                return [BayTypeGrElectrical];
            }

            if (string.Equals(code, TeamCodeBodyCare, StringComparison.OrdinalIgnoreCase) ||
                specialty == "BODY_PAINT" ||
                specialty == "INSURANCE_REPAIR")
            {
                return [BayTypeSpBodyPaint];
            }

            if (string.Equals(code, TeamCodeCarCare, StringComparison.OrdinalIgnoreCase) ||
                specialty == "CAR_CARE" ||
                specialty == "DETAILING")
            {
                return [BayTypeSpDetailing];
            }

            return [BayTypeSpDiagnose];
        }

        internal static bool IsOperationalBayAvailable(string? operationalStatus)
        {
            var normalized = (operationalStatus ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return true;
            }

            return normalized switch
            {
                nameof(BayOperationalStatusCode.BROKEN) => false,
                nameof(BayOperationalStatusCode.UNDER_MAINTENANCE) => false,
                _ => true,
            };
        }
    }
}
