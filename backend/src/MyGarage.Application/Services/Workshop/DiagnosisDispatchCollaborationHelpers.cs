using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MyGarage.DTOs.Requests;
using MyGarage.Enums;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisAssignmentStateHelpers;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    internal static class DiagnosisDispatchCollaborationHelpers
    {
        private const string DispatchCollaborationPrefix = "[DISPATCH_COLLAB]";

        internal sealed class DispatchCollaborationTeamAssignmentMeta
        {
            public long TeamId { get; set; }
            public long? TechnicianId { get; set; }
            public long? AssistantTechnicianId { get; set; }
            public int? RequestedTechnicianCount { get; set; }
        }

        internal sealed class DispatchCollaborationMeta
        {
            public long? PrimaryTeamId { get; set; }
            public List<long> TeamIds { get; set; } = new();
            public List<DispatchCollaborationTeamAssignmentMeta> TeamAssignments { get; set; } = new();
        }

        internal sealed class NormalizedSupportingAssignment
        {
            public long TeamId { get; set; }
            public long? TechnicianId { get; set; }
            public long? AssistantTechnicianId { get; set; }
            public int? RequestedTechnicianCount { get; set; }
        }

        internal static IReadOnlyList<long> NormalizeTeamIds(IEnumerable<long>? teamIds)
        {
            return (teamIds ?? Array.Empty<long>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();
        }

        internal static int? NormalizeRequestedTechnicianCount(int? requestedTechnicianCount)
        {
            if (!requestedTechnicianCount.HasValue || requestedTechnicianCount.Value <= 0)
            {
                return null;
            }

            return Math.Clamp(requestedTechnicianCount.Value, 1, 8);
        }

        internal static IReadOnlyList<NormalizedSupportingAssignment> NormalizeSupportingAssignments(
            IEnumerable<DiagnosisSupportingTeamAssignmentDto>? assignments,
            IEnumerable<long>? fallbackTeamIds,
            long? primaryTeamId)
        {
            var byTeam = new Dictionary<long, NormalizedSupportingAssignment>();

            foreach (var fallbackTeamId in NormalizeTeamIds(fallbackTeamIds))
            {
                if (primaryTeamId.HasValue && fallbackTeamId == primaryTeamId.Value)
                {
                    continue;
                }

                byTeam[fallbackTeamId] = new NormalizedSupportingAssignment
                {
                    TeamId = fallbackTeamId,
                    TechnicianId = null,
                    AssistantTechnicianId = null,
                    RequestedTechnicianCount = null
                };
            }

            foreach (var assignment in assignments ?? Array.Empty<DiagnosisSupportingTeamAssignmentDto>())
            {
                if (assignment == null || assignment.TeamId <= 0)
                {
                    continue;
                }

                if (primaryTeamId.HasValue && assignment.TeamId == primaryTeamId.Value)
                {
                    continue;
                }

                var technicianId = assignment.TechnicianId > 0 ? assignment.TechnicianId : (long?)null;
                var assistantTechnicianId = assignment.AssistantTechnicianId.HasValue && assignment.AssistantTechnicianId.Value > 0
                    ? assignment.AssistantTechnicianId
                    : null;
                if (technicianId.HasValue && assistantTechnicianId == technicianId)
                {
                    assistantTechnicianId = null;
                }

                byTeam[assignment.TeamId] = new NormalizedSupportingAssignment
                {
                    TeamId = assignment.TeamId,
                    TechnicianId = technicianId,
                    AssistantTechnicianId = assistantTechnicianId,
                    RequestedTechnicianCount = NormalizeRequestedTechnicianCount(assignment.RequestedTechnicianCount)
                };
            }

            return byTeam.Values
                .OrderBy(x => x.TeamId)
                .ToList();
        }

        internal static string? BuildDispatchCollaborationMeta(
            long? primaryTeamId,
            IReadOnlyList<DispatchCollaborationTeamAssignmentMeta>? teamAssignments)
        {
            var normalizedAssignments = (teamAssignments ?? Array.Empty<DispatchCollaborationTeamAssignmentMeta>())
                .Where(x => x != null && x.TeamId > 0)
                .GroupBy(x => x.TeamId)
                .Select(group =>
                {
                    var chosen = group.First();
                    var leadId = chosen.TechnicianId.HasValue && chosen.TechnicianId.Value > 0
                        ? chosen.TechnicianId
                        : null;
                    var assistantId = chosen.AssistantTechnicianId.HasValue && chosen.AssistantTechnicianId.Value > 0
                        ? chosen.AssistantTechnicianId
                        : null;
                    if (leadId.HasValue && assistantId == leadId)
                    {
                        assistantId = null;
                    }

                    return new DispatchCollaborationTeamAssignmentMeta
                    {
                        TeamId = group.Key,
                        TechnicianId = leadId,
                        AssistantTechnicianId = assistantId,
                        RequestedTechnicianCount = NormalizeRequestedTechnicianCount(chosen.RequestedTechnicianCount)
                    };
                })
                .OrderBy(x => x.TeamId)
                .ToList();

            if (primaryTeamId.HasValue && primaryTeamId.Value > 0 && normalizedAssignments.All(x => x.TeamId != primaryTeamId.Value))
            {
                normalizedAssignments.Insert(0, new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = primaryTeamId.Value,
                    RequestedTechnicianCount = null
                });
            }

            var normalizedTeamIds = normalizedAssignments
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            if (primaryTeamId.HasValue && primaryTeamId.Value > 0 && !normalizedTeamIds.Contains(primaryTeamId.Value))
            {
                normalizedTeamIds.Insert(0, primaryTeamId.Value);
            }

            // Không cần ghi metadata khi chỉ có duy nhất tổ chính và không có thông tin phối hợp.
            if (normalizedTeamIds.Count <= 1)
            {
                return null;
            }

            var payload = new DispatchCollaborationMeta
            {
                PrimaryTeamId = primaryTeamId.HasValue && primaryTeamId.Value > 0 ? primaryTeamId : null,
                TeamIds = normalizedTeamIds,
                TeamAssignments = normalizedAssignments
            };

            return JsonSerializer.Serialize(payload);
        }

        private static DispatchCollaborationMeta? NormalizeCollaborationMeta(DispatchCollaborationMeta? parsed)
        {
            if (parsed == null)
            {
                return null;
            }

            var normalizedPrimary = parsed.PrimaryTeamId.HasValue && parsed.PrimaryTeamId.Value > 0
                ? parsed.PrimaryTeamId
                : null;

            var normalizedTeamIds = NormalizeTeamIds(parsed.TeamIds).ToList();
            var normalizedAssignments = (parsed.TeamAssignments ?? new List<DispatchCollaborationTeamAssignmentMeta>())
                .Where(x => x != null && x.TeamId > 0)
                .GroupBy(x => x.TeamId)
                .Select(group =>
                {
                    var chosen = group.First();
                    var leadId = chosen.TechnicianId.HasValue && chosen.TechnicianId.Value > 0
                        ? chosen.TechnicianId
                        : null;
                    var assistantId = chosen.AssistantTechnicianId.HasValue && chosen.AssistantTechnicianId.Value > 0
                        ? chosen.AssistantTechnicianId
                        : null;
                    if (leadId.HasValue && assistantId == leadId)
                    {
                        assistantId = null;
                    }

                    return new DispatchCollaborationTeamAssignmentMeta
                    {
                        TeamId = group.Key,
                        TechnicianId = leadId,
                        AssistantTechnicianId = assistantId,
                        RequestedTechnicianCount = NormalizeRequestedTechnicianCount(chosen.RequestedTechnicianCount)
                    };
                })
                .OrderBy(x => x.TeamId)
                .ToList();

            normalizedTeamIds = normalizedTeamIds
                .Concat(normalizedAssignments.Select(x => x.TeamId))
                .Distinct()
                .ToList();

            if (normalizedPrimary.HasValue && !normalizedTeamIds.Contains(normalizedPrimary.Value))
            {
                normalizedTeamIds.Insert(0, normalizedPrimary.Value);
            }

            if (normalizedTeamIds.Count == 0 && !normalizedPrimary.HasValue)
            {
                return null;
            }

            return new DispatchCollaborationMeta
            {
                PrimaryTeamId = normalizedPrimary,
                TeamIds = normalizedTeamIds,
                TeamAssignments = normalizedAssignments
            };
        }

        private static DispatchCollaborationMeta? TryParseDispatchCollaborationJson(string? rawJson)
        {
            var text = (rawJson ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<DispatchCollaborationMeta>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return NormalizeCollaborationMeta(parsed);
            }
            catch
            {
                return null;
            }
        }

        internal static DispatchCollaborationMeta? TryParseDispatchCollaborationNote(string? dispatchCollaborationMeta, string? legacyCollaborationNote = null)
        {
            var parsedNewMeta = TryParseDispatchCollaborationJson(dispatchCollaborationMeta);
            if (parsedNewMeta != null)
            {
                return parsedNewMeta;
            }

            var legacyText = (legacyCollaborationNote ?? string.Empty).Trim();
            if (!legacyText.StartsWith(DispatchCollaborationPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var legacyJson = legacyText.Substring(DispatchCollaborationPrefix.Length).Trim();
            return TryParseDispatchCollaborationJson(legacyJson);
        }

        internal static string ResolveDiagnosisWorkflowStatus(
            Diagnosis latestDiagnosis,
            bool hasAssigned,
            bool isEscalated,
            bool hasPendingTeamLeaderApproval,
            string? teamLeaderApprovalStatus,
            string? finalApprovalStatus)
        {
            static bool EqualsCode(string? source, string code) =>
                string.Equals(source?.Trim(), code, StringComparison.OrdinalIgnoreCase);

            var diagnosisStatus = (latestDiagnosis.Status ?? string.Empty).Trim();
            if (EqualsCode(diagnosisStatus, nameof(DiagnosisStatus.CANCELED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.CANCELED);
            }

            if (EqualsCode(finalApprovalStatus, nameof(ApprovalDecisionStatusCode.APPROVED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.FOREMAN_APPROVED_LOCKED);
            }

            if (EqualsCode(finalApprovalStatus, nameof(ApprovalDecisionStatusCode.REJECTED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.FOREMAN_REJECTED_REDIAGNOSE);
            }

            if (EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.REJECTED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.TL_REJECTED_REDIAGNOSE);
            }

            if (EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.BYPASSED)) ||
                EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.ESCALATED)) ||
                isEscalated)
            {
                return nameof(DiagnosisWorkflowStatusCode.ESCALATED_TO_FOREMAN);
            }

            if (EqualsCode(teamLeaderApprovalStatus, nameof(ApprovalDecisionStatusCode.APPROVED)))
            {
                return nameof(DiagnosisWorkflowStatusCode.TL_APPROVED);
            }

            if (hasPendingTeamLeaderApproval)
            {
                return nameof(DiagnosisWorkflowStatusCode.SUBMITTED);
            }

            if (latestDiagnosis.SubmittedAt.HasValue)
            {
                return nameof(DiagnosisWorkflowStatusCode.SUBMITTED);
            }

            return hasAssigned
                ? nameof(DiagnosisWorkflowStatusCode.IN_PROGRESS)
                : nameof(DiagnosisWorkflowStatusCode.NEW);
        }

        internal static string? ResolveDispatchBucket(
            string? diagnosisStatus,
            string? workflowStatus,
            string? viewMode,
            bool hasAssignedTeamId,
            bool hasTechnicianId,
            int pendingMinutes)
        {
            static bool EqualsCode(string? source, string code) =>
                string.Equals(source?.Trim(), code, StringComparison.OrdinalIgnoreCase);

            var mode = (viewMode ?? string.Empty).Trim().ToUpperInvariant();
            var normalizedDiagnosisStatus = (diagnosisStatus ?? string.Empty).Trim().ToUpperInvariant();
            var normalizedWorkflowStatus = (workflowStatus ?? string.Empty).Trim().ToUpperInvariant();

            if (EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.CANCELED)))
            {
                return null;
            }

            // Lịch sử của tổ trưởng.
            if (EqualsCode(mode, "TEAM_LEADER") &&
                (EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.TL_APPROVED)) ||
                 EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.TL_REJECTED_REDIAGNOSE))))
            {
                return null;
            }

            // Lịch sử của quản đốc.
            if ((EqualsCode(mode, "FOREMAN") || EqualsCode(mode, "MANAGER")) &&
                (EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.FOREMAN_APPROVED_LOCKED)) ||
                 EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.FOREMAN_REJECTED_REDIAGNOSE))))
            {
                return null;
            }

            // Cần duyệt: diagnosis COMPLETED + workflow SUBMITTED.
            if (EqualsCode(normalizedDiagnosisStatus, DiagnosisStatusCompleted) &&
                EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.SUBMITTED)))
            {
                return "APPROVAL_BUCKET";
            }

            // Cần duyệt của quản đốc/manager: tổ trưởng đã duyệt hoặc đã đẩy lên quản đốc.
            if ((EqualsCode(mode, "FOREMAN") || EqualsCode(mode, "MANAGER")) &&
                EqualsCode(normalizedDiagnosisStatus, DiagnosisStatusCompleted) &&
                (EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.TL_APPROVED)) ||
                 EqualsCode(normalizedWorkflowStatus, nameof(DiagnosisWorkflowStatusCode.ESCALATED_TO_FOREMAN))))
            {
                return "APPROVAL_BUCKET";
            }

            // Đang chẩn đoán: ASSIGNED và đã có thợ.
            if (EqualsCode(normalizedDiagnosisStatus, DiagnosisStatusAssigned) && hasTechnicianId)
            {
                return "YELLOW";
            }

            // Mới tiếp nhận / Cần can thiệp: PENDING, có tổ, chưa có thợ.
            if (EqualsCode(normalizedDiagnosisStatus, DiagnosisStatusPending) && hasAssignedTeamId && !hasTechnicianId)
            {
                if (pendingMinutes >= UnclaimedEscalationMinutes)
                {
                    return "RED";
                }

                return "WHITE";
            }

            // Trạng thái không nằm trong dashboard điều phối hiện tại sẽ đưa về lịch sử/bỏ qua.
            if (EqualsCode(normalizedDiagnosisStatus, DiagnosisStatusCompleted))
            {
                return null;
            }

            return null;
        }
    }
}
