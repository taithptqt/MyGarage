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
        private const string DiagnosisStatusDraft = nameof(DiagnosisStatus.DRAFT);
        private const string DiagnosisStatusPending = nameof(DiagnosisStatus.PENDING);
        private const string DiagnosisStatusAssigned = nameof(DiagnosisStatus.ASSIGNED);
        private const string DiagnosisStatusCompleted = nameof(DiagnosisStatus.COMPLETED);

        private const string CancellationStatusNone = nameof(DiagnosisCancellationStatusCode.NONE);

        private const string ServiceOrderStatusDiagnosing = nameof(ServiceOrderLifecycleStatus.DIAGNOSING);
        private const string ServiceOrderStatusCheckedIn = nameof(ServiceOrderLifecycleStatus.CHECKED_IN);
        private const string StaffStatusActive = nameof(StaffStatusCode.ACTIVE);

        private const int UnclaimedEscalationMinutes = 10;
        private const int GoldenCancelWindowMinutes = 10;
        private const int DiagnosisSlaMinutes = 45;
        private const string DispatchCollaborationPrefix = "[DISPATCH_COLLAB]";

        private sealed class DispatchCollaborationTeamAssignmentMeta
        {
            public long TeamId { get; set; }
            public long? TechnicianId { get; set; }
            public long? AssistantTechnicianId { get; set; }
            public int? RequestedTechnicianCount { get; set; }
        }

        private sealed class DispatchCollaborationMeta
        {
            public long? PrimaryTeamId { get; set; }
            public List<long> TeamIds { get; set; } = new();
            public List<DispatchCollaborationTeamAssignmentMeta> TeamAssignments { get; set; } = new();
        }

        private sealed class NormalizedSupportingAssignment
        {
            public long TeamId { get; set; }
            public long? TechnicianId { get; set; }
            public long? AssistantTechnicianId { get; set; }
            public int? RequestedTechnicianCount { get; set; }
        }

        private static string? NormalizeDispatchTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return null;
            }

            return rawTag.Trim().ToUpperInvariant();
        }

        private static IReadOnlyList<string> SplitDispatchTags(string? rawTag)
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

        private static IReadOnlyList<long> NormalizeTeamIds(IEnumerable<long>? teamIds)
        {
            return (teamIds ?? Array.Empty<long>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();
        }

        private static int? NormalizeRequestedTechnicianCount(int? requestedTechnicianCount)
        {
            if (!requestedTechnicianCount.HasValue || requestedTechnicianCount.Value <= 0)
            {
                return null;
            }

            return Math.Clamp(requestedTechnicianCount.Value, 1, 8);
        }

        private static IReadOnlyList<NormalizedSupportingAssignment> NormalizeSupportingAssignments(
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

        private static string? BuildDispatchCollaborationMeta(
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

        private static DispatchCollaborationMeta? TryParseDispatchCollaborationNote(string? dispatchCollaborationMeta, string? legacyFinalApprovalNote = null)
        {
            var parsedNewMeta = TryParseDispatchCollaborationJson(dispatchCollaborationMeta);
            if (parsedNewMeta != null)
            {
                return parsedNewMeta;
            }

            var legacyText = (legacyFinalApprovalNote ?? string.Empty).Trim();
            if (!legacyText.StartsWith(DispatchCollaborationPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var legacyJson = legacyText.Substring(DispatchCollaborationPrefix.Length).Trim();
            return TryParseDispatchCollaborationJson(legacyJson);
        }

        public async Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder, string? dispatchTag = null)
        {
            var dispatchTags = SplitDispatchTags(dispatchTag);
            var hasDispatchFilter = dispatchTags.Count > 0;

            var candidateTeams = await _context.Teams.AsNoTracking()
                .Where(x => x.BranchId == serviceOrder.ExecuteBranchId)
                .Where(x => x.LeaderId.HasValue)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .OrderBy(x => x.Id)
                .Select(x => new { x.LeaderId, x.SpecialtyTag })
                .ToListAsync();

            var leaderByDispatchTag = candidateTeams
                .Where(x =>
                {
                    if (!hasDispatchFilter)
                    {
                        return true;
                    }

                    var specialty = NormalizeDispatchTag(x.SpecialtyTag);
                    return specialty != null && dispatchTags.Contains(specialty, StringComparer.OrdinalIgnoreCase);
                })
                .Select(x => x.LeaderId)
                .FirstOrDefault();

            if (leaderByDispatchTag.HasValue)
            {
                var leaderExists = await _context.Staffs.AnyAsync(x => x.Id == leaderByDispatchTag.Value);
                if (leaderExists)
                {
                    return leaderByDispatchTag.Value;
                }
            }

            if (serviceOrder.ServiceAdvisorId.HasValue)
            {
                var advisorExists = await _context.Staffs.AnyAsync(x => x.Id == serviceOrder.ServiceAdvisorId.Value);
                if (advisorExists)
                {
                    return serviceOrder.ServiceAdvisorId.Value;
                }
            }

            var firstBranchStaffId = await _context.Staffs
                .Where(x => x.BranchId == serviceOrder.ExecuteBranchId)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();
            if (firstBranchStaffId > 0)
            {
                return firstBranchStaffId;
            }

            var firstStaffId = await _context.Staffs
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            if (firstStaffId <= 0)
            {
                throw new InvalidOperationException("Khong co nhan su trong he thong de khoi tao yeu cau chan doan.");
            }

            return firstStaffId;
        }

        public async Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(long serviceOrderId, long technicianId, IReadOnlyList<string> symptoms, string? dispatchTag = null)
        {
            var now = DateTime.UtcNow;
            var normalizedDispatchTag = NormalizeDispatchTag(dispatchTag);

            var items = (symptoms ?? Array.Empty<string>())
                .Select(symptom => new Diagnosis
                {
                    ServiceOrderId = serviceOrderId,
                    TechnicianId = technicianId,
                    AssistantTechnicianId = null,
                    AssignedBayId = null,
                    Symptom = symptom,
                    Status = DiagnosisStatusPending,
                    DispatchTag = normalizedDispatchTag,
                    AssignedTeamId = null,
                    AssignedAt = null,
                    StartedAt = null,
                    SupportRequestedAt = null,
                    SupportRequestedBy = null,
                    SupportRequestNote = null,
                    CancellationStatus = CancellationStatusNone,
                    CancellationReason = null,
                    CancellationRequestedAt = null,
                    CancellationRequestedBy = null,
                    CancellationDecisionAt = null,
                    CancellationDecisionBy = null,
                    CancellationDecisionNote = null,
                    FinalApprovedByManagerId = null,
                    FinalApprovedAt = null,
                    FinalApprovalNote = null,
                    DispatchCollaborationMeta = null,
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            _context.Diagnoses.AddRange(items);
            await _context.SaveChangesAsync();
            return items;
        }

        public async Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status)
        {
            serviceOrder.Status = status;
            serviceOrder.UpdatedAt = DateTime.UtcNow;
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId)
        {
            return await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => IsPendingLikeStatus(x.Status) || x.Status == DiagnosisStatusAssigned)
                .ToListAsync();
        }

        public async Task<bool> TechnicianExistsAsync(long technicianId)
        {
            return await _context.Staffs.AnyAsync(x => x.Id == technicianId);
        }

        public async Task<Team?> GetTeamByIdAsync(long teamId)
        {
            return await _context.Teams.FirstOrDefaultAsync(x => x.Id == teamId);
        }

        public async Task<Staff?> GetStaffByIdAsync(long staffId)
        {
            return await _context.Staffs.FirstOrDefaultAsync(x => x.Id == staffId);
        }

        public async Task<long?> ResolveTeamIdForTechnicianAsync(long technicianId)
        {
            return await _context.Staffs.AsNoTracking()
                .Where(x => x.Id == technicianId)
                .Select(x => x.TeamId)
                .FirstOrDefaultAsync();
        }

        public async Task<bool> IsTechnicianInTeamAsync(long technicianId, long teamId)
        {
            return await _context.Staffs.AsNoTracking().AnyAsync(x => x.Id == technicianId && x.TeamId == teamId);
        }

        public async Task<bool> IsBayInBranchAsync(long bayId, long branchId)
        {
            return await _context.Bays.AsNoTracking()
                .AnyAsync(x => x.Id == bayId && x.BranchId == branchId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false));
        }

        public async Task<bool> IsDispatchTagCompatibleAsync(string? dispatchTag, long? teamId)
        {
            if (!teamId.HasValue)
            {
                return true;
            }

            var dispatchTags = SplitDispatchTags(dispatchTag);
            if (dispatchTags.Count == 0)
            {
                return true;
            }

            var teamSpecialtyTag = NormalizeDispatchTag(await _context.Teams.AsNoTracking()
                .Where(x => x.Id == teamId.Value)
                .Select(x => x.SpecialtyTag)
                .FirstOrDefaultAsync());

            if (teamSpecialtyTag == null)
            {
                return true;
            }

            return dispatchTags.Contains(teamSpecialtyTag, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<long?> ResolveDefaultTechnicianForTeamAsync(long teamId)
        {
            var teamLeaderId = await _context.Teams.AsNoTracking()
                .Where(x => x.Id == teamId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
                .Select(x => x.LeaderId)
                .FirstOrDefaultAsync();

            if (teamLeaderId.HasValue)
            {
                var leaderIsValid = await _context.Staffs.AsNoTracking()
                    .AnyAsync(x =>
                        x.Id == teamLeaderId.Value &&
                        x.TeamId == teamId &&
                        (!x.IsDeleted.HasValue || x.IsDeleted.Value == false) &&
                        (string.IsNullOrEmpty(x.Status) || string.Equals(x.Status, StaffStatusActive, StringComparison.OrdinalIgnoreCase)));

                if (leaderIsValid)
                {
                    return teamLeaderId.Value;
                }
            }

            var firstTeamTechnicianId = await _context.Staffs.AsNoTracking()
                .Where(x => x.TeamId == teamId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => string.IsNullOrEmpty(x.Status) || string.Equals(x.Status, StaffStatusActive, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();

            return firstTeamTechnicianId > 0 ? firstTeamTechnicianId : null;
        }

        public async Task AssignDiagnosesAsync(
            IEnumerable<Diagnosis> diagnoses,
            long technicianId,
            long? assistantTechnicianId,
            long? teamId,
            IReadOnlyList<long>? supportingTeamIds,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments,
            long? bayId,
            bool managerOverride)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var resolvedPrimaryTeamId = teamId ?? await ResolveTeamIdForTechnicianAsync(technicianId);
            var normalizedSupportingAssignments = NormalizeSupportingAssignments(
                supportingAssignments,
                supportingTeamIds,
                resolvedPrimaryTeamId);

            var supportingTeamIdsResolved = normalizedSupportingAssignments
                .Select(x => x.TeamId)
                .Distinct()
                .ToList();

            var technicianByTeam = new Dictionary<long, long>();
            var assistantByTeam = new Dictionary<long, long?>();
            var collaborationAssignments = new List<DispatchCollaborationTeamAssignmentMeta>();

            if (resolvedPrimaryTeamId.HasValue && resolvedPrimaryTeamId.Value > 0)
            {
                technicianByTeam[resolvedPrimaryTeamId.Value] = technicianId;
                assistantByTeam[resolvedPrimaryTeamId.Value] = assistantTechnicianId;
                collaborationAssignments.Add(new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = resolvedPrimaryTeamId.Value,
                    TechnicianId = technicianId,
                    AssistantTechnicianId = assistantTechnicianId
                });
            }

            foreach (var supportingAssignment in normalizedSupportingAssignments)
            {
                var resolvedSupportLead = supportingAssignment.TechnicianId;
                var supportAssistant = supportingAssignment.AssistantTechnicianId;
                if (resolvedSupportLead.HasValue && supportAssistant.HasValue && supportAssistant.Value == resolvedSupportLead.Value)
                {
                    supportAssistant = null;
                }

                if (resolvedSupportLead.HasValue && resolvedSupportLead.Value > 0)
                {
                    technicianByTeam[supportingAssignment.TeamId] = resolvedSupportLead.Value;
                    assistantByTeam[supportingAssignment.TeamId] = supportAssistant;
                }

                collaborationAssignments.Add(new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = supportingAssignment.TeamId,
                    TechnicianId = resolvedSupportLead,
                    AssistantTechnicianId = supportAssistant,
                    RequestedTechnicianCount = supportingAssignment.RequestedTechnicianCount
                });
            }

            var collaborationTeamIds = new List<long>();
            if (resolvedPrimaryTeamId.HasValue && resolvedPrimaryTeamId.Value > 0)
            {
                collaborationTeamIds.Add(resolvedPrimaryTeamId.Value);
            }
            collaborationTeamIds.AddRange(supportingTeamIdsResolved);
            collaborationTeamIds = collaborationTeamIds.Distinct().ToList();

            var assignmentTeamIds = technicianByTeam
                .Keys
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var collaborationByDiagnosis = assignmentTeamIds.Count == 0
                ? new List<long?> { resolvedPrimaryTeamId }
                : assignmentTeamIds.Select(x => (long?)x).ToList();

            var collaborationMeta = BuildDispatchCollaborationMeta(resolvedPrimaryTeamId, collaborationAssignments);

            var sortedDiagnoses = list
                .OrderBy(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ThenBy(x => x.Id)
                .ToList();

            for (var index = 0; index < sortedDiagnoses.Count; index++)
            {
                var diagnosis = sortedDiagnoses[index];
                var assignedTeamId = collaborationByDiagnosis[index % collaborationByDiagnosis.Count];

                var resolvedLeadTechnicianId = assignedTeamId.HasValue && technicianByTeam.TryGetValue(assignedTeamId.Value, out var mappedTechnicianId)
                    ? mappedTechnicianId
                    : technicianId;
                var resolvedAssistantTechnicianId = assignedTeamId.HasValue && assistantByTeam.TryGetValue(assignedTeamId.Value, out var mappedAssistantTechnicianId)
                    ? mappedAssistantTechnicianId
                    : (resolvedPrimaryTeamId.HasValue && assignedTeamId == resolvedPrimaryTeamId ? assistantTechnicianId : null);
                if (resolvedAssistantTechnicianId.HasValue && resolvedAssistantTechnicianId.Value == resolvedLeadTechnicianId)
                {
                    resolvedAssistantTechnicianId = null;
                }

                diagnosis.TechnicianId = resolvedLeadTechnicianId;
                diagnosis.AssistantTechnicianId = resolvedAssistantTechnicianId;
                diagnosis.Status = DiagnosisStatusAssigned;
                diagnosis.AssignedTeamId = assignedTeamId;
                diagnosis.AssignedBayId = bayId;
                diagnosis.AssignedAt ??= now;
                diagnosis.StartedAt = null;

                diagnosis.ApprovedByForemanId = null;
                diagnosis.FinalApprovedByManagerId = null;
                diagnosis.FinalApprovedAt = null;
                diagnosis.FinalApprovalNote = null;
                diagnosis.DispatchCollaborationMeta = collaborationMeta;

                diagnosis.CancellationStatus = CancellationStatusNone;
                diagnosis.CancellationReason = null;
                diagnosis.CancellationRequestedAt = null;
                diagnosis.CancellationRequestedBy = null;
                diagnosis.CancellationDecisionAt = null;
                diagnosis.CancellationDecisionBy = null;
                diagnosis.CancellationDecisionNote = null;

                if (managerOverride)
                {
                    diagnosis.SupportRequestedAt = null;
                    diagnosis.SupportRequestedBy = null;
                    diagnosis.SupportRequestNote = null;
                }

                diagnosis.UpdatedAt = now;
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task StartDiagnosesAsync(IEnumerable<Diagnosis> diagnoses, long technicianId, long? bayId)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                if (diagnosis.Status == DiagnosisStatusCompleted)
                {
                    continue;
                }

                diagnosis.Status = DiagnosisStatusAssigned;
                diagnosis.StartedAt ??= now;
                diagnosis.AssignedAt ??= now;
                diagnosis.AssignedBayId = bayId ?? diagnosis.AssignedBayId;
                diagnosis.UpdatedBy = technicianId;
                diagnosis.UpdatedAt = now;
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task<DiagnosisDispatchSuggestionDto> BuildDispatchSuggestionAsync(long serviceOrderId, long teamId)
        {
            var team = await _context.Teams.AsNoTracking()
                .Where(x => x.Id == teamId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
                .Select(x => new { x.Id, x.Name, x.BranchId })
                .FirstOrDefaultAsync();

            if (team == null)
            {
                throw new InvalidOperationException("Khong tim thay to ky thuat duoc chon.");
            }

            var technicians = await _context.Staffs.AsNoTracking()
                .Where(x => x.TeamId == teamId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => string.IsNullOrEmpty(x.Status) || string.Equals(x.Status, StaffStatusActive, StringComparison.OrdinalIgnoreCase))
                .Select(x => new { x.Id, x.FullName, x.SkillLevel })
                .ToListAsync();

            var technicianIds = technicians.Select(x => x.Id).Distinct().ToList();
            var loads = new Dictionary<long, int>();

            if (technicianIds.Count > 0)
            {
                var loadRows = await _context.Diagnoses.AsNoTracking()
                    .Where(x => x.Status == DiagnosisStatusAssigned)
                    .Where(x => technicianIds.Contains(x.TechnicianId) || (x.AssistantTechnicianId.HasValue && technicianIds.Contains(x.AssistantTechnicianId.Value)))
                    .Select(x => new
                    {
                        x.ServiceOrderId,
                        x.TechnicianId,
                        x.AssistantTechnicianId
                    })
                    .ToListAsync();

                var loadBuckets = new Dictionary<long, HashSet<long>>();
                foreach (var techId in technicianIds)
                {
                    loadBuckets[techId] = new HashSet<long>();
                }

                foreach (var row in loadRows)
                {
                    if (loadBuckets.TryGetValue(row.TechnicianId, out var leadOrders))
                    {
                        leadOrders.Add(row.ServiceOrderId);
                    }

                    if (row.AssistantTechnicianId.HasValue && loadBuckets.TryGetValue(row.AssistantTechnicianId.Value, out var assistantOrders))
                    {
                        assistantOrders.Add(row.ServiceOrderId);
                    }
                }

                foreach (var bucket in loadBuckets)
                {
                    loads[bucket.Key] = bucket.Value.Count;
                }
            }

            var sortedTechnicians = technicians
                .Select(x => new DiagnosisTechnicianLoadDto
                {
                    TechnicianId = x.Id,
                    TechnicianName = x.FullName,
                    SkillLevel = x.SkillLevel,
                    ActiveDiagnosisCount = loads.TryGetValue(x.Id, out var count) ? count : 0
                })
                .OrderBy(x => x.ActiveDiagnosisCount)
                .ThenByDescending(x => x.SkillLevel ?? 0)
                .ThenBy(x => x.TechnicianName ?? string.Empty)
                .ToList();

            var suggestedLead = sortedTechnicians.FirstOrDefault();
            var suggestedAssistant = sortedTechnicians.Skip(1).FirstOrDefault();

            var suggestedBay = await _context.Bays.AsNoTracking()
                .Where(x => x.BranchId == team.BranchId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .OrderBy(x => x.CurrentOccupiedVehicleId.HasValue ? 1 : 0)
                .ThenBy(x => x.Code)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.CurrentOccupiedVehicleId,
                    x.OperationalStatus
                })
                .ToListAsync();

            var freeBay = suggestedBay
                .Where(x => !x.CurrentOccupiedVehicleId.HasValue && IsOperationalBayAvailable(x.OperationalStatus))
                .OrderBy(x => x.Code ?? x.Name ?? string.Empty)
                .FirstOrDefault();

            return new DiagnosisDispatchSuggestionDto
            {
                ServiceOrderId = serviceOrderId,
                TeamId = team.Id,
                TeamName = team.Name,
                SuggestedLeadTechnicianId = suggestedLead?.TechnicianId,
                SuggestedLeadTechnicianName = suggestedLead?.TechnicianName,
                SuggestedAssistantTechnicianId = suggestedAssistant?.TechnicianId,
                SuggestedAssistantTechnicianName = suggestedAssistant?.TechnicianName,
                SuggestedBayId = freeBay?.Id,
                SuggestedBayCode = freeBay?.Code ?? freeBay?.Name,
                TechnicianLoads = sortedTechnicians
            };
        }

        public async Task MarkSupportRequestedAsync(
            IEnumerable<Diagnosis> diagnoses,
            long requestedByStaffId,
            string? note,
            IReadOnlyList<DiagnosisSupportingTeamAssignmentDto>? supportingAssignments = null)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var latest = list
                .OrderByDescending(x => x.UpdatedAt ?? x.AssignedAt ?? x.CreatedAt ?? DateTime.MinValue)
                .FirstOrDefault();
            var existingMeta = latest == null
                ? null
                : TryParseDispatchCollaborationNote(latest.DispatchCollaborationMeta, latest.FinalApprovalNote);

            var primaryTeamId = existingMeta?.PrimaryTeamId
                ?? list
                    .Where(x => x.AssignedTeamId.HasValue && x.AssignedTeamId.Value > 0)
                    .Select(x => x.AssignedTeamId!.Value)
                    .FirstOrDefault();
            if (primaryTeamId.HasValue && primaryTeamId.Value <= 0)
            {
                primaryTeamId = null;
            }

            var requestedAssignments = NormalizeSupportingAssignments(
                supportingAssignments,
                null,
                primaryTeamId);
            var requestedByTeam = requestedAssignments
                .Where(x => x.TeamId > 0)
                .ToDictionary(x => x.TeamId, x => x);

            var mergedByTeam = new Dictionary<long, DispatchCollaborationTeamAssignmentMeta>();
            foreach (var existing in existingMeta?.TeamAssignments ?? new List<DispatchCollaborationTeamAssignmentMeta>())
            {
                if (existing == null || existing.TeamId <= 0)
                {
                    continue;
                }

                if (primaryTeamId.HasValue && existing.TeamId == primaryTeamId.Value)
                {
                    continue;
                }

                mergedByTeam[existing.TeamId] = new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = existing.TeamId,
                    TechnicianId = existing.TechnicianId,
                    AssistantTechnicianId = existing.AssistantTechnicianId,
                    RequestedTechnicianCount = NormalizeRequestedTechnicianCount(existing.RequestedTechnicianCount)
                };
            }

            foreach (var requested in requestedByTeam.Values)
            {
                var existingRequestedCount = mergedByTeam.TryGetValue(requested.TeamId, out var existing)
                    ? existing.RequestedTechnicianCount
                    : null;

                mergedByTeam[requested.TeamId] = new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = requested.TeamId,
                    TechnicianId = null,
                    AssistantTechnicianId = null,
                    RequestedTechnicianCount = NormalizeRequestedTechnicianCount(
                        requested.RequestedTechnicianCount
                        ?? existingRequestedCount
                        ?? 1)
                };
            }

            var collaborationAssignments = new List<DispatchCollaborationTeamAssignmentMeta>();
            if (primaryTeamId.HasValue && primaryTeamId.Value > 0)
            {
                var primaryDiagnosis = list
                    .Where(x => x.AssignedTeamId == primaryTeamId.Value)
                    .OrderByDescending(x => x.AssignedAt ?? x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                    .FirstOrDefault();

                collaborationAssignments.Add(new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = primaryTeamId.Value,
                    TechnicianId = primaryDiagnosis != null && primaryDiagnosis.TechnicianId > 0 ? primaryDiagnosis.TechnicianId : null,
                    AssistantTechnicianId = primaryDiagnosis?.AssistantTechnicianId,
                    RequestedTechnicianCount = null
                });
            }

            collaborationAssignments.AddRange(mergedByTeam.Values);
            var collaborationMeta = BuildDispatchCollaborationMeta(primaryTeamId, collaborationAssignments);

            foreach (var diagnosis in list)
            {
                if (diagnosis.Status == DiagnosisStatusCompleted)
                {
                    continue;
                }

                diagnosis.SupportRequestedAt = now;
                diagnosis.SupportRequestedBy = requestedByStaffId;
                diagnosis.SupportRequestNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                diagnosis.DispatchCollaborationMeta = collaborationMeta;
                diagnosis.UpdatedAt = now;
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task AssignSupportingTeamSelectionAsync(
            IEnumerable<Diagnosis> diagnoses,
            long teamId,
            long teamLeaderId,
            long technicianId,
            long? assistantTechnicianId,
            string? note)
        {
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var latest = list
                .OrderByDescending(x => x.UpdatedAt ?? x.AssignedAt ?? x.CreatedAt ?? DateTime.MinValue)
                .FirstOrDefault();
            var collaborationMeta = latest == null
                ? null
                : TryParseDispatchCollaborationNote(latest.DispatchCollaborationMeta, latest.FinalApprovalNote);
            if (collaborationMeta == null)
            {
                throw new InvalidOperationException("Khong tim thay thong tin phoi hop de cap nhat nhan su ho tro.");
            }

            var primaryTeamId = collaborationMeta.PrimaryTeamId;
            if (primaryTeamId.HasValue && primaryTeamId.Value == teamId)
            {
                throw new InvalidOperationException("To chinh khong duoc dung luong chon nhan su ho tro to phu.");
            }

            if (!collaborationMeta.TeamIds.Contains(teamId))
            {
                throw new InvalidOperationException("To duoc chon khong nam trong danh sach yeu cau phoi hop.");
            }

            var byTeam = (collaborationMeta.TeamAssignments ?? new List<DispatchCollaborationTeamAssignmentMeta>())
                .Where(x => x != null && x.TeamId > 0)
                .GroupBy(x => x.TeamId)
                .ToDictionary(x => x.Key, x => x.First());

            if (!byTeam.TryGetValue(teamId, out var targetAssignment))
            {
                targetAssignment = new DispatchCollaborationTeamAssignmentMeta
                {
                    TeamId = teamId,
                    TechnicianId = null,
                    AssistantTechnicianId = null,
                    RequestedTechnicianCount = null
                };
            }

            targetAssignment.TechnicianId = technicianId > 0 ? technicianId : null;
            targetAssignment.AssistantTechnicianId = assistantTechnicianId.HasValue && assistantTechnicianId.Value > 0
                ? assistantTechnicianId
                : null;
            if (targetAssignment.AssistantTechnicianId.HasValue && targetAssignment.AssistantTechnicianId == targetAssignment.TechnicianId)
            {
                targetAssignment.AssistantTechnicianId = null;
            }
            targetAssignment.RequestedTechnicianCount = NormalizeRequestedTechnicianCount(targetAssignment.RequestedTechnicianCount ?? 1);
            byTeam[teamId] = targetAssignment;

            var orderedAssignments = byTeam
                .Values
                .OrderBy(x => x.TeamId)
                .ToList();
            var nextCollaborationMeta = BuildDispatchCollaborationMeta(primaryTeamId, orderedAssignments);

            var hasPendingSupportSelection = orderedAssignments.Any(x =>
                (!primaryTeamId.HasValue || x.TeamId != primaryTeamId.Value) &&
                NormalizeRequestedTechnicianCount(x.RequestedTechnicianCount).GetValueOrDefault() > 0 &&
                (!x.TechnicianId.HasValue || x.TechnicianId.Value <= 0));

            var now = DateTime.UtcNow;
            foreach (var diagnosis in list)
            {
                if (diagnosis.Status == DiagnosisStatusCompleted)
                {
                    continue;
                }

                diagnosis.DispatchCollaborationMeta = nextCollaborationMeta;
                diagnosis.UpdatedBy = teamLeaderId;
                diagnosis.UpdatedAt = now;

                if (!string.IsNullOrWhiteSpace(note))
                {
                    diagnosis.SupportRequestNote = note.Trim();
                }

                if (!hasPendingSupportSelection)
                {
                    diagnosis.SupportRequestedAt = null;
                    diagnosis.SupportRequestedBy = null;
                }
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task RequestCancellationAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string reason)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            if (list.Any(x => x.Status != DiagnosisStatusAssigned))
            {
                throw new InvalidOperationException("Chi cho phep gui huy lenh khi dang o trang thai ASSIGNED.");
            }

            if (list.Any(x => !IsWithinGoldenCancelWindow(x, now)))
            {
                throw new InvalidOperationException("To truong chi duoc gui huy trong 10 phut dau sau khi nhan xe.");
            }

            foreach (var diagnosis in list)
            {
                diagnosis.CancellationStatus = CancellationStatusNone;
                diagnosis.CancellationReason = reason;
                diagnosis.CancellationRequestedAt = now;
                diagnosis.CancellationRequestedBy = requestedByStaffId;
                diagnosis.CancellationDecisionAt = null;
                diagnosis.CancellationDecisionBy = null;
                diagnosis.CancellationDecisionNote = null;
                diagnosis.UpdatedAt = now;
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task ApplyCancellationDecisionAsync(ServiceOrder serviceOrder, IEnumerable<Diagnosis> diagnoses, bool approve, long managerId, string? note)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                if (!IsCancellationPending(diagnosis))
                {
                    continue;
                }

                if (approve)
                {
                    var placeholderTechnicianId = await ResolvePlaceholderTechnicianIdAsync(serviceOrder, diagnosis.DispatchTag);
                    diagnosis.Status = DiagnosisStatusPending;
                    diagnosis.TechnicianId = placeholderTechnicianId;
                    diagnosis.AssistantTechnicianId = null;
                    diagnosis.AssignedTeamId = null;
                    diagnosis.AssignedBayId = null;
                    diagnosis.AssignedAt = null;
                    diagnosis.StartedAt = null;
                    diagnosis.SupportRequestedAt = null;
                    diagnosis.SupportRequestedBy = null;
                    diagnosis.SupportRequestNote = null;
                    diagnosis.ApprovedByForemanId = null;
                    diagnosis.FinalApprovedByManagerId = null;
                    diagnosis.FinalApprovedAt = null;
                    diagnosis.FinalApprovalNote = null;
                    diagnosis.DispatchCollaborationMeta = null;
                }

                diagnosis.CancellationStatus = CancellationStatusNone;

                diagnosis.CancellationDecisionBy = managerId;
                diagnosis.CancellationDecisionAt = now;
                diagnosis.CancellationDecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                diagnosis.UpdatedAt = now;
            }

            if (approve)
            {
                serviceOrder.Status = ServiceOrderStatusDiagnosing;
                serviceOrder.UpdatedAt = now;
                _context.ServiceOrders.Update(serviceOrder);
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task ApproveTechnicalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? note)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                diagnosis.ApprovedByForemanId = teamLeaderId;
                diagnosis.UpdatedBy = teamLeaderId;
                diagnosis.UpdatedAt = now;

                if (!string.IsNullOrWhiteSpace(note))
                {
                    diagnosis.SupportRequestNote = note.Trim();
                }
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task ApproveFinalAsync(IEnumerable<Diagnosis> diagnoses, long managerId, string? note)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                diagnosis.FinalApprovedByManagerId = managerId;
                diagnosis.FinalApprovedAt = now;
                diagnosis.FinalApprovalNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                diagnosis.UpdatedBy = managerId;
                diagnosis.UpdatedAt = now;
            }

            _context.Diagnoses.UpdateRange(list);
            var serviceOrderId = list[0].ServiceOrderId;
            var order = await _context.ServiceOrders.FirstOrDefaultAsync(x => x.Id == serviceOrderId);
            if (order != null)
            {
                order.Status = ServiceOrderStatusDiagnosing;
                order.UpdatedAt = now;
                _context.ServiceOrders.Update(order);
            }
            await _context.SaveChangesAsync();
        }

        public async Task RecallDiagnosesAsync(ServiceOrder serviceOrder, IReadOnlyList<Diagnosis> activeDiagnoses)
        {
            var activeIds = activeDiagnoses.Select(x => x.Id).ToList();
            await RemoveRecommendationsByDiagnosisIdsAsync(activeIds);
            _context.Diagnoses.RemoveRange(activeDiagnoses);

            if (serviceOrder.Status == ServiceOrderStatusDiagnosing)
            {
                serviceOrder.Status = ServiceOrderStatusCheckedIn;
            }

            serviceOrder.UpdatedAt = DateTime.UtcNow;
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();
        }

        public async Task<Diagnosis?> GetDiagnosisEntityAsync(long id)
        {
            return await _context.Diagnoses.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task CompleteDiagnosisAsync(Diagnosis diagnosis)
        {
            diagnosis.Status = DiagnosisStatusCompleted;
            diagnosis.DiagnosedAt = DateTime.UtcNow;
            diagnosis.CancellationStatus = CancellationStatusNone;
            diagnosis.UpdatedAt = DateTime.UtcNow;
            _context.Diagnoses.Update(diagnosis);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AreAllDiagnosesCompletedAsync(long serviceOrderId)
        {
            var allStatus = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Select(x => x.Status)
                .ToListAsync();
            return allStatus.Count > 0 && allStatus.All(s => s == DiagnosisStatusCompleted);
        }

        private static bool IsOperationalBayAvailable(string? operationalStatus)
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

        private static bool IsCancellationPending(Diagnosis diagnosis)
        {
            return diagnosis.CancellationRequestedAt.HasValue && !diagnosis.CancellationDecisionAt.HasValue;
        }

        private static bool IsPendingLikeStatus(string? status)
        {
            return status == DiagnosisStatusPending || status == DiagnosisStatusDraft;
        }

        private static bool IsWithinGoldenCancelWindow(Diagnosis diagnosis, DateTime now)
        {
            var assignedAt = diagnosis.AssignedAt ?? diagnosis.UpdatedAt ?? diagnosis.CreatedAt;
            if (!assignedAt.HasValue)
            {
                return false;
            }

            var minutes = (now - assignedAt.Value).TotalMinutes;
            return minutes >= 0 && minutes <= GoldenCancelWindowMinutes;
        }
    }
}
