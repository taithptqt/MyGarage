using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        private const string ServiceOrderStatusInspecting = nameof(ServiceOrderLifecycleStatus.INSPECTING);
        private const string ServiceOrderStatusNew = nameof(ServiceOrderLifecycleStatus.NEW);
        private const string StaffStatusActive = nameof(StaffStatusCode.ACTIVE);

        private const int UnclaimedEscalationMinutes = 10;
        private const int GoldenCancelWindowMinutes = 10;
        private const int DiagnosisSlaMinutes = 45;
        private const string DispatchCollaborationPrefix = "[DISPATCH_COLLAB]";

        private sealed class DispatchCollaborationMeta
        {
            public long? PrimaryTeamId { get; set; }
            public List<long> TeamIds { get; set; } = new();
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

        private static string? BuildDispatchCollaborationNote(long? primaryTeamId, IReadOnlyList<long>? teamIds)
        {
            var normalizedTeamIds = NormalizeTeamIds(teamIds);
            if (primaryTeamId.HasValue && primaryTeamId.Value > 0 && !normalizedTeamIds.Contains(primaryTeamId.Value))
            {
                normalizedTeamIds = new[] { primaryTeamId.Value }.Concat(normalizedTeamIds).ToList();
            }

            if (normalizedTeamIds.Count <= 1)
            {
                return null;
            }

            var payload = new DispatchCollaborationMeta
            {
                PrimaryTeamId = primaryTeamId.HasValue && primaryTeamId.Value > 0 ? primaryTeamId : null,
                TeamIds = normalizedTeamIds.ToList()
            };

            return $"{DispatchCollaborationPrefix}{JsonSerializer.Serialize(payload)}";
        }

        private static DispatchCollaborationMeta? TryParseDispatchCollaborationNote(string? rawNote)
        {
            var text = (rawNote ?? string.Empty).Trim();
            if (!text.StartsWith(DispatchCollaborationPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var json = text.Substring(DispatchCollaborationPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<DispatchCollaborationMeta>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed == null)
                {
                    return null;
                }

                var normalizedTeamIds = NormalizeTeamIds(parsed.TeamIds);
                var normalizedPrimary = parsed.PrimaryTeamId.HasValue && parsed.PrimaryTeamId.Value > 0
                    ? parsed.PrimaryTeamId
                    : null;

                if (normalizedPrimary.HasValue && !normalizedTeamIds.Contains(normalizedPrimary.Value))
                {
                    normalizedTeamIds = new[] { normalizedPrimary.Value }.Concat(normalizedTeamIds).ToList();
                }

                if (normalizedTeamIds.Count == 0 && !normalizedPrimary.HasValue)
                {
                    return null;
                }

                return new DispatchCollaborationMeta
                {
                    PrimaryTeamId = normalizedPrimary,
                    TeamIds = normalizedTeamIds.ToList()
                };
            }
            catch
            {
                return null;
            }
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
            var normalizedSupportingTeamIds = NormalizeTeamIds(supportingTeamIds)
                .Where(x => !resolvedPrimaryTeamId.HasValue || x != resolvedPrimaryTeamId.Value)
                .ToList();

            var collaborationTeamIds = new List<long>();
            if (resolvedPrimaryTeamId.HasValue && resolvedPrimaryTeamId.Value > 0)
            {
                collaborationTeamIds.Add(resolvedPrimaryTeamId.Value);
            }
            collaborationTeamIds.AddRange(normalizedSupportingTeamIds);
            collaborationTeamIds = collaborationTeamIds.Distinct().ToList();

            var collaborationByDiagnosis = collaborationTeamIds.Count == 0
                ? new List<long?> { resolvedPrimaryTeamId }
                : collaborationTeamIds.Select(x => (long?)x).ToList();

            var supportTechnicianByTeam = new Dictionary<long, long>();
            foreach (var supportTeamId in normalizedSupportingTeamIds)
            {
                var resolvedTechnicianForTeam = await ResolveDefaultTechnicianForTeamAsync(supportTeamId);
                supportTechnicianByTeam[supportTeamId] = resolvedTechnicianForTeam ?? technicianId;
            }

            var collaborationNote = BuildDispatchCollaborationNote(resolvedPrimaryTeamId, collaborationTeamIds);

            var sortedDiagnoses = list
                .OrderBy(x => x.CreatedAt ?? x.UpdatedAt ?? DateTime.MinValue)
                .ThenBy(x => x.Id)
                .ToList();

            for (var index = 0; index < sortedDiagnoses.Count; index++)
            {
                var diagnosis = sortedDiagnoses[index];
                var assignedTeamId = collaborationByDiagnosis[index % collaborationByDiagnosis.Count];

                var isPrimaryTeamAssignment = resolvedPrimaryTeamId.HasValue && assignedTeamId == resolvedPrimaryTeamId;
                var supportingTechnicianId = assignedTeamId.HasValue && supportTechnicianByTeam.TryGetValue(assignedTeamId.Value, out var mappedTechnicianId)
                    ? mappedTechnicianId
                    : technicianId;

                diagnosis.TechnicianId = isPrimaryTeamAssignment ? technicianId : supportingTechnicianId;
                diagnosis.AssistantTechnicianId = isPrimaryTeamAssignment ? assistantTechnicianId : null;
                diagnosis.Status = DiagnosisStatusAssigned;
                diagnosis.AssignedTeamId = assignedTeamId;
                diagnosis.AssignedBayId = bayId;
                diagnosis.AssignedAt ??= now;
                diagnosis.StartedAt = null;

                diagnosis.ApprovedByForemanId = null;
                diagnosis.FinalApprovedByManagerId = null;
                diagnosis.FinalApprovedAt = null;
                diagnosis.FinalApprovalNote = collaborationNote;

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

        public async Task MarkSupportRequestedAsync(IEnumerable<Diagnosis> diagnoses, long requestedByStaffId, string? note)
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

                diagnosis.SupportRequestedAt = now;
                diagnosis.SupportRequestedBy = requestedByStaffId;
                diagnosis.SupportRequestNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                diagnosis.UpdatedAt = now;
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
                }

                diagnosis.CancellationStatus = CancellationStatusNone;

                diagnosis.CancellationDecisionBy = managerId;
                diagnosis.CancellationDecisionAt = now;
                diagnosis.CancellationDecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                diagnosis.UpdatedAt = now;
            }

            if (approve)
            {
                serviceOrder.Status = ServiceOrderStatusInspecting;
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
                order.Status = ServiceOrderStatusInspecting;
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

            if (serviceOrder.Status == ServiceOrderStatusInspecting)
            {
                serviceOrder.Status = ServiceOrderStatusNew;
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
