using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    internal static class DiagnosisCommandHelpers
    {
        internal const string DiagnosisStatusPending = nameof(DiagnosisStatus.PENDING);
        internal const string DiagnosisStatusAssigned = nameof(DiagnosisStatus.ASSIGNED);
        internal const string DiagnosisStatusCompleted = nameof(DiagnosisStatus.COMPLETED);
        internal const string DiagnosisStatusCanceled = nameof(DiagnosisStatus.CANCELED);
        internal const string ServiceOrderStatusDiagnosing = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);

        private static readonly IReadOnlyDictionary<string, string[]> DispatchTagKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ENGINE_CHASSIS"] = new[]
            {
                "dong co", "gam", "phanh", "hop so", "ly hop", "con", "lai", "treo",
                "truyen dong", "ro ri dau", "nong may", "chet may"
            },
            ["ELECTRICAL_AC"] = new[]
            {
                "dien", "ac quy", "de may", "de no", "cam bien", "dieu hoa", "may lanh",
                "quat gio", "den", "kinh dien", "gat mua", "am thanh", "man hinh"
            },
            ["BODY_PAINT"] = new[]
            {
                "dong son", "than vo", "vo xe", "tray", "xuoc", "mop", "kinh",
                "noi that", "ngoai that", "danh bong", "cham soc", "rua xe", "son"
            },
            ["CAR_CARE"] = new[]
            {
                "cham soc", "rua xe", "detail", "detailing", "noi that", "ngoai that", "ceramic", "ppf"
            },
            ["MAINTENANCE"] = new[]
            {
                "bao duong", "dinh ky", "thay dau", "loc dau", "loc gio", "mini check",
                "quick check", "kiem tra tong quat", "tong quat", "checklist"
            }
        };

        internal static string NormalizeStatus(string? rawStatus)
        {
            var normalized = (rawStatus ?? string.Empty).Trim().ToUpperInvariant();
            return Enum.TryParse<DiagnosisStatus>(normalized, true, out var parsed)
                ? parsed.ToString()
                : DiagnosisStatusPending;
        }

        internal static string? NormalizeDispatchTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return null;
            }

            return rawTag.Trim().ToUpperInvariant();
        }

        internal static bool IsFinalDecisionMade(string? finalStatus) =>
            string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.APPROVED), StringComparison.OrdinalIgnoreCase) ||
            string.Equals(finalStatus, nameof(ApprovalDecisionStatusCode.REJECTED), StringComparison.OrdinalIgnoreCase);

        internal static IReadOnlyList<DiagnosisSupportingTeamAssignmentDto> NormalizeSupportingAssignments(
            IEnumerable<long>? teamIds,
            IEnumerable<DiagnosisSupportingTeamAssignmentDto>? assignments,
            long? primaryTeamId)
        {
            var byTeam = new Dictionary<long, DiagnosisSupportingTeamAssignmentDto>();

            foreach (var teamId in teamIds ?? Array.Empty<long>())
            {
                if (teamId <= 0 || (primaryTeamId.HasValue && teamId == primaryTeamId.Value))
                {
                    continue;
                }

                byTeam[teamId] = new DiagnosisSupportingTeamAssignmentDto
                {
                    TeamId = teamId,
                    TechnicianId = 0,
                    AssistantTechnicianId = null,
                    RequestedTechnicianCount = null
                };
            }

            foreach (var assignment in assignments ?? Array.Empty<DiagnosisSupportingTeamAssignmentDto>())
            {
                if (assignment == null || assignment.TeamId <= 0 || (primaryTeamId.HasValue && assignment.TeamId == primaryTeamId.Value))
                {
                    continue;
                }

                byTeam[assignment.TeamId] = new DiagnosisSupportingTeamAssignmentDto
                {
                    TeamId = assignment.TeamId,
                    TechnicianId = assignment.TechnicianId,
                    AssistantTechnicianId = assignment.AssistantTechnicianId,
                    RequestedTechnicianCount = assignment.RequestedTechnicianCount
                };
            }

            return byTeam.Values
                .OrderBy(x => x.TeamId)
                .ToList();
        }

        internal static List<string> ExtractSymptomsFromNotes(string? customerNotes)
        {
            var fallback = new List<string>();
            if (string.IsNullOrWhiteSpace(customerNotes))
            {
                return fallback;
            }

            var notes = customerNotes.Trim();
            try
            {
                using var doc = JsonDocument.Parse(notes);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var values = root.EnumerateArray()
                        .Where(x => x.ValueKind == JsonValueKind.String)
                        .Select(x => (x.GetString() ?? string.Empty).Trim())
                        .Where(IsMeaningfulSymptom)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return values.Count > 0 ? values : fallback;
                }

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("symptoms", out var symptomsEl) && symptomsEl.ValueKind == JsonValueKind.Array)
                    {
                        var values = symptomsEl.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.String)
                            .Select(x => (x.GetString() ?? string.Empty).Trim())
                            .Where(IsMeaningfulSymptom)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        return values.Count > 0 ? values : fallback;
                    }

                    if (root.TryGetProperty("note", out var noteEl) && noteEl.ValueKind == JsonValueKind.String)
                    {
                        var note = (noteEl.GetString() ?? string.Empty).Trim();
                        return IsMeaningfulSymptom(note) ? new List<string> { note } : fallback;
                    }
                }
            }
            catch
            {
                // Legacy non-JSON format.
            }

            return IsMeaningfulSymptom(notes)
                ? new List<string> { notes }
                : fallback;
        }

        internal static List<string> FilterSymptomsByDispatchTag(IReadOnlyList<string>? symptoms, string? dispatchTag)
        {
            var normalizedSymptoms = (symptoms ?? Array.Empty<string>())
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedSymptoms.Count == 0)
            {
                return new List<string>();
            }

            var dispatchTags = (dispatchTag ?? string.Empty)
                .Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (dispatchTags.Count == 0)
            {
                return normalizedSymptoms;
            }

            var filtered = normalizedSymptoms
                .Where(symptom =>
                {
                    var normalizedSymptom = NormalizeKeywordText(symptom);
                    return dispatchTags.Any(tag => IsSymptomCompatibleWithDispatchTag(normalizedSymptom, tag));
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return filtered.Count > 0 ? filtered : normalizedSymptoms;
        }

        private static bool IsMeaningfulSymptom(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = NormalizeKeywordText(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            if (normalized == "khong co" ||
                normalized == "khong co ghi chu" ||
                normalized == "khong co ghi chu gi" ||
                normalized == "khong co mo ta")
            {
                return false;
            }

            if (normalized.StartsWith("[tiep nhan]", StringComparison.Ordinal) &&
                normalized.Contains("khong co", StringComparison.Ordinal))
            {
                return false;
            }

            if (normalized.StartsWith("[flow:", StringComparison.Ordinal) ||
                normalized.StartsWith("[cluster:", StringComparison.Ordinal) ||
                normalized.StartsWith("[dispatch", StringComparison.Ordinal) ||
                normalized.StartsWith("[auto_dispatch", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static bool IsSymptomCompatibleWithDispatchTag(string normalizedSymptom, string dispatchTag)
        {
            if (!DispatchTagKeywords.TryGetValue(dispatchTag, out var keywords) || keywords.Length == 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(normalizedSymptom))
            {
                return false;
            }

            return keywords.Any(keyword => normalizedSymptom.Contains(keyword, StringComparison.Ordinal));
        }

        private static string NormalizeKeywordText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var normalized = raw.Trim().ToLowerInvariant();
            normalized = normalized.Replace('\u0111', 'd');

            var decomposed = normalized.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        internal static async Task<string?> ValidatePrimaryAndAssistantTechniciansAsync(
            IDiagnosisService diagnosisService,
            long technicianId,
            long? requestedTeamId,
            long? resolvedTeamId,
            long? assistantTechnicianId,
            string teamMismatchMessage)
        {
            var technicianExists = await diagnosisService.TechnicianExistsAsync(technicianId);
            if (!technicianExists)
            {
                return "Kỹ thuật viên không tồn tại.";
            }

            if (requestedTeamId.HasValue)
            {
                var inTeam = await diagnosisService.IsTechnicianInTeamAsync(technicianId, requestedTeamId.Value);
                if (!inTeam)
                {
                    return teamMismatchMessage;
                }
            }

            if (assistantTechnicianId.HasValue)
            {
                var assistantExists = await diagnosisService.TechnicianExistsAsync(assistantTechnicianId.Value);
                if (!assistantExists)
                {
                    return "Thợ phụ không tồn tại.";
                }

                if (resolvedTeamId.HasValue)
                {
                    var assistantInTeam = await diagnosisService.IsTechnicianInTeamAsync(assistantTechnicianId.Value, resolvedTeamId.Value);
                    if (!assistantInTeam)
                    {
                        return "Thợ phụ không thuộc tổ được giao.";
                    }
                }
            }

            return null;
        }

        internal static async Task<string?> ValidateSupportingAssignmentsWithTechniciansAsync(
            IDiagnosisService diagnosisService,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto> normalizedSupportingAssignments,
            long? executeBranchId)
        {
            foreach (var supportingAssignment in normalizedSupportingAssignments)
            {
                if (supportingAssignment.TechnicianId <= 0)
                {
                    return "Mỗi tổ phối hợp phải chỉ định thợ chính.";
                }

                if (supportingAssignment.AssistantTechnicianId.HasValue &&
                    supportingAssignment.AssistantTechnicianId.Value == supportingAssignment.TechnicianId)
                {
                    return "Thợ phụ của tổ phối hợp không được trùng thợ chính.";
                }

                var supportingTeamId = supportingAssignment.TeamId;
                var supportingTeam = await diagnosisService.GetTeamByIdAsync(supportingTeamId);
                if (supportingTeam == null || (supportingTeam.IsDeleted.HasValue && supportingTeam.IsDeleted.Value))
                {
                    return $"Tổ phối hợp #{supportingTeamId} không tồn tại.";
                }

                if (executeBranchId.HasValue && supportingTeam.BranchId != executeBranchId.Value)
                {
                    return "Tổ phối hợp phải thuộc cùng chi nhánh với lệnh sửa chữa.";
                }

                var supportingLeadExists = await diagnosisService.TechnicianExistsAsync(supportingAssignment.TechnicianId);
                if (!supportingLeadExists)
                {
                    return $"Thợ chính của tổ phối hợp #{supportingTeamId} không tồn tại.";
                }

                var supportingLeadInTeam = await diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.TechnicianId, supportingTeamId);
                if (!supportingLeadInTeam)
                {
                    return $"Thợ chính của tổ phối hợp #{supportingTeamId} không thuộc tổ được chọn.";
                }

                if (supportingAssignment.AssistantTechnicianId.HasValue)
                {
                    var supportingAssistantExists = await diagnosisService.TechnicianExistsAsync(supportingAssignment.AssistantTechnicianId.Value);
                    if (!supportingAssistantExists)
                    {
                        return $"Thợ phụ của tổ phối hợp #{supportingTeamId} không tồn tại.";
                    }

                    var supportingAssistantInTeam = await diagnosisService.IsTechnicianInTeamAsync(supportingAssignment.AssistantTechnicianId.Value, supportingTeamId);
                    if (!supportingAssistantInTeam)
                    {
                        return $"Thợ phụ của tổ phối hợp #{supportingTeamId} không thuộc tổ được chọn.";
                    }
                }
            }

            return null;
        }
    }
}
