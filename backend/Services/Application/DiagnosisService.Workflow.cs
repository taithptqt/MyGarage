using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class DiagnosisService
    {
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
        private const int DiagnosisQueueDefaultMinutes = 30;
        private const int DiagnosisQueueDefaultBufferMinutes = 15;
        private const string AutoDiagnosisQueueSlotMarker = "[AUTO_DIAG_QUEUE]";
        private const string DispatchCollaborationPrefix = "[DISPATCH_COLLAB]";
        private const string AutoDispatchWarningPrefix = "[AUTO_DISPATCH_WARNING]";
        private const string TeamCodeFastMaint = "FAST_MAINT";
        private const string TeamCodeEngChas1 = "ENG_CHAS_1";
        private const string TeamCodeEngChas2 = "ENG_CHAS_2";
        private const string TeamCodeElecAc = "ELEC_AC";
        private const string TeamCodeBodyCare = "BODY_CARE";
        private const string TeamCodeCarCare = "CAR_CARE";

        private const string BayTypeQsLift = "QS_LIFT";
        private const string BayTypeQsFlat = "QS_FLAT";
        private const string BayTypeGrLift = "GR_LIFT";
        private const string BayTypeGrAlign = "GR_ALIGN";
        private const string BayTypeGrElectrical = "GR_ELECTRICAL";
        private const string BayTypeSpDiagnose = "SP_DIAGNOSE";
        private const string BayTypeSpBodyPaint = "SP_BODY_PAINT";
        private const string BayTypeSpDetailing = "SP_DETAILING";

        private static readonly string[] QuickOrderCategories =
        [
            "MAINTENANCE",
            "QUICK_SERVICE"
        ];

        private static readonly string[] DiagnoseFirstStatuses =
        [
            nameof(ServiceOrderLifecycleStatus.NEW),
            nameof(ServiceOrderLifecycleStatus.CHECKED_IN),
            nameof(ServiceOrderLifecycleStatus.DIAGNOSING)
        ];

        private static readonly IReadOnlyDictionary<string, string> DiagnosisCodeDispatchTagMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DG-MINI"] = "MAINTENANCE",
            ["DG-GEN"] = "MAINTENANCE",
            ["DG-SCAN"] = "ELECTRICAL_AC",
            ["DG-ENG"] = "ENGINE_CHASSIS",
            ["DG-ELEC"] = "ELECTRICAL_AC",
            ["DG-AC"] = "ELECTRICAL_AC",
            ["DG-NOISE"] = "ENGINE_CHASSIS"
        };

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
            _ = dispatchTag;

            if (serviceOrder.ReceptionistId.HasValue)
            {
                var receptionistId = await TryResolveFrontOfficeStaffIdAsync(serviceOrder.ReceptionistId.Value);
                if (receptionistId.HasValue)
                {
                    return receptionistId.Value;
                }
            }

            if (serviceOrder.ServiceAdvisorId.HasValue)
            {
                var advisorId = await TryResolveFrontOfficeStaffIdAsync(serviceOrder.ServiceAdvisorId.Value);
                if (advisorId.HasValue)
                {
                    return advisorId.Value;
                }
            }

            if (serviceOrder.CreatedBy.HasValue)
            {
                var createdById = await TryResolveFrontOfficeStaffIdAsync(serviceOrder.CreatedBy.Value);
                if (createdById.HasValue)
                {
                    return createdById.Value;
                }
            }

            var branchFrontOfficeId = await _context.Staffs.AsNoTracking()
                .Where(x => x.BranchId == serviceOrder.ExecuteBranchId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == StaffStatusActive)
                .Where(x => !x.TeamId.HasValue || x.TeamId.Value <= 0)
                .OrderBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync();
            if (branchFrontOfficeId > 0)
            {
                return branchFrontOfficeId;
            }

            throw new InvalidOperationException("Khong tim thay nhan su tiep nhan/CVDV de tao hang cho chan doan.");
        }

        private async Task<long?> TryResolveFrontOfficeStaffIdAsync(long staffId)
        {
            if (staffId <= 0)
            {
                return null;
            }

            var candidate = await _context.Staffs.AsNoTracking()
                .Where(x => x.Id == staffId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == StaffStatusActive)
                .Select(x => new
                {
                    x.Id,
                    x.TeamId
                })
                .FirstOrDefaultAsync();

            if (candidate == null)
            {
                return null;
            }

            if (candidate.TeamId.HasValue && candidate.TeamId.Value > 0)
            {
                return null;
            }

            return candidate.Id;
        }

        public async Task<IReadOnlyList<Diagnosis>> CreatePendingDiagnosesAsync(
            long serviceOrderId,
            long? technicianId,
            IReadOnlyList<string> symptoms,
            string? dispatchTag = null,
            long? assignedTeamId = null,
            long? assignedBayId = null)
        {
            var now = VietnamTime.Now;
            var normalizedDispatchTag = NormalizeDispatchTag(dispatchTag);
            var normalizedSymptoms = (symptoms ?? Array.Empty<string>())
                .Select(symptom => (symptom ?? string.Empty).Trim())
                .Where(IsMeaningfulQueueSymptom)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalizedSymptoms.Count == 0)
            {
                normalizedSymptoms.Add(string.Empty);
            }

            var items = normalizedSymptoms
                .Select(symptom => new Diagnosis
                {
                    ServiceOrderId = serviceOrderId,
                    TechnicianId = technicianId.HasValue && technicianId.Value > 0 ? technicianId : null,
                    AssistantTechnicianId = null,
                    AssignedBayId = assignedBayId.HasValue && assignedBayId.Value > 0 ? assignedBayId : null,
                    Symptom = string.IsNullOrWhiteSpace(symptom) ? null : symptom,
                    Status = DiagnosisStatusPending,
                    DispatchTag = normalizedDispatchTag,
                    AssignedTeamId = assignedTeamId.HasValue && assignedTeamId.Value > 0 ? assignedTeamId : null,
                    AssignedAt = (assignedTeamId.HasValue && assignedTeamId.Value > 0) || (assignedBayId.HasValue && assignedBayId.Value > 0) ? now : null,
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
                    AssignedByTeamLeaderId = null,
                    ApprovedByForemanAt = null,
                    ApprovedByTeamLeaderAt = null,
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

        private static bool IsMeaningfulQueueSymptom(string? symptom)
        {
            var value = (symptom ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.ToLowerInvariant();
            if (normalized.StartsWith("[flow:", StringComparison.Ordinal) ||
                normalized.StartsWith("[cluster:", StringComparison.Ordinal) ||
                normalized.StartsWith("[dispatch", StringComparison.Ordinal) ||
                normalized.StartsWith("[auto_dispatch", StringComparison.Ordinal))
            {
                return false;
            }

            if (normalized == "khong co" ||
                normalized == "không có" ||
                normalized == "khong co ghi chu" ||
                normalized == "không có ghi chú" ||
                normalized == "khong co ghi chu gi" ||
                normalized == "không có ghi chú gì" ||
                normalized == "khong co mo ta" ||
                normalized == "không có mô tả")
            {
                return false;
            }

            if ((normalized.StartsWith("[tiep nhan]", StringComparison.Ordinal) ||
                 normalized.StartsWith("[tiếp nhận]", StringComparison.Ordinal)) &&
                (normalized.Contains("khong co", StringComparison.Ordinal) || normalized.Contains("không có", StringComparison.Ordinal)))
            {
                return false;
            }

            return true;
        }

        public async Task UpdateServiceOrderStatusAsync(ServiceOrder serviceOrder, string status)
        {
            serviceOrder.Status = status;
            serviceOrder.UpdatedAt = VietnamTime.Now;
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<Diagnosis>> GetActiveDiagnosesAsync(long serviceOrderId)
        {
            return await _context.Diagnoses
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x =>
                    x.Status == DiagnosisStatusPending ||
                    x.Status == DiagnosisStatusAssigned)
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

        public async Task<long?> ResolveDiagnoseBayIdForBranchAsync(long branchId, long? preferredBayId = null)
        {
            if (branchId <= 0)
            {
                return null;
            }

            var configuredDiagnoseBayCodes = ResolveConfiguredDiagnoseBayCodes(branchId);
            var hasConfiguredCodes = configuredDiagnoseBayCodes.Count > 0;
            if (!hasConfiguredCodes)
            {
                return null;
            }

            long? validatedPreferredBayId = null;
            if (preferredBayId.HasValue && preferredBayId.Value > 0)
            {
                validatedPreferredBayId = await _context.Bays.AsNoTracking()
                    .Where(x => x.Id == preferredBayId.Value && x.BranchId == branchId)
                    .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                    .Where(x => x.IsActive == true)
                    .Where(x => !hasConfiguredCodes || configuredDiagnoseBayCodes.Contains((x.Code ?? string.Empty).Trim().ToUpper()))
                    .Select(x => (long?)x.Id)
                    .FirstOrDefaultAsync();
            }

            var branchBays = await _context.Bays.AsNoTracking()
                .Where(x => x.BranchId == branchId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => x.IsActive == true)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.BayType,
                    x.CurrentOccupiedVehicleId,
                    x.OperationalStatus
                })
                .ToListAsync();

            var diagnoseBays = branchBays
                .Where(x => string.Equals(NormalizeToken(x.BayType), BayTypeSpDiagnose, StringComparison.OrdinalIgnoreCase))
                .Where(x =>
                {
                    if (!hasConfiguredCodes)
                    {
                        return true;
                    }

                    var code = (x.Code ?? string.Empty).Trim().ToUpperInvariant();
                    return configuredDiagnoseBayCodes.Contains(code, StringComparer.OrdinalIgnoreCase);
                })
                .OrderBy(x =>
                {
                    if (!hasConfiguredCodes)
                    {
                        return int.MaxValue;
                    }

                    var code = (x.Code ?? string.Empty).Trim().ToUpperInvariant();
                    var index = configuredDiagnoseBayCodes.IndexOf(code);
                    return index >= 0 ? index : int.MaxValue;
                })
                .ThenBy(x => x.Code ?? x.Name ?? string.Empty)
                .Take(hasConfiguredCodes ? 2 : int.MaxValue)
                .ToList();

            var availableDiagnoseBay = diagnoseBays
                .FirstOrDefault(x => !x.CurrentOccupiedVehicleId.HasValue && IsOperationalBayAvailable(x.OperationalStatus));
            if (availableDiagnoseBay != null)
            {
                return availableDiagnoseBay.Id;
            }

            var firstDiagnoseBay = diagnoseBays.FirstOrDefault();
            if (firstDiagnoseBay != null)
            {
                return firstDiagnoseBay.Id;
            }

            return validatedPreferredBayId;
        }

        public async Task<WorkSlot?> ScheduleDiagnosisQueueSlotAsync(long serviceOrderId, int diagnoseMinutes, int bufferMinutes = 15, long? preferredBayId = null)
        {
            if (serviceOrderId <= 0)
            {
                return null;
            }

            var serviceOrder = await _context.ServiceOrders.AsNoTracking()
                .Where(x => x.Id == serviceOrderId)
                .Select(x => new
                {
                    x.Id,
                    x.ExecuteBranchId,
                    x.CustomerId,
                    x.VehicleId,
                    x.AppointmentId,
                    x.CheckInTime,
                    x.CreatedAt
                })
                .FirstOrDefaultAsync();
            if (serviceOrder == null || serviceOrder.ExecuteBranchId <= 0)
            {
                return null;
            }

            var effectiveDiagnoseMinutes = Math.Max(5, diagnoseMinutes > 0 ? diagnoseMinutes : DiagnosisQueueDefaultMinutes);
            var effectiveBufferMinutes = Math.Max(0, bufferMinutes > 0 ? bufferMinutes : DiagnosisQueueDefaultBufferMinutes);
            var totalMinutes = effectiveDiagnoseMinutes + effectiveBufferMinutes;
            var now = VietnamTime.Now;
            var planningAnchor = now;

            var configuredDiagnoseBayCodes = ResolveConfiguredDiagnoseBayCodes(serviceOrder.ExecuteBranchId);
            var hasConfiguredCodes = configuredDiagnoseBayCodes.Count > 0;
            if (!hasConfiguredCodes)
            {
                return null;
            }

            var diagnoseBays = await _context.Bays.AsNoTracking()
                .Where(x => x.BranchId == serviceOrder.ExecuteBranchId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => x.IsActive == true)
                .Where(x => x.BayType == BayTypeSpDiagnose)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name
                })
                .OrderBy(x => x.Code ?? x.Name ?? string.Empty)
                .ToListAsync();
            if (diagnoseBays.Count == 0)
            {
                return null;
            }
            if (hasConfiguredCodes)
            {
                diagnoseBays = diagnoseBays
                    .Where(x => configuredDiagnoseBayCodes.Contains((x.Code ?? string.Empty).Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }
            if (diagnoseBays.Count == 0)
            {
                return null;
            }
            diagnoseBays = diagnoseBays
                .OrderBy(x =>
                {
                    var code = (x.Code ?? string.Empty).Trim().ToUpperInvariant();
                    var index = configuredDiagnoseBayCodes.IndexOf(code);
                    return index >= 0 ? index : int.MaxValue;
                })
                .ThenBy(x => x.Code ?? x.Name ?? string.Empty)
                .Take(2)
                .ToList();
            if (diagnoseBays.Count == 0)
            {
                return null;
            }

            var diagnoseBayIds = diagnoseBays.Select(x => x.Id).ToList();
            var existingSlot = await _context.WorkSlots
                .Where(x => x.ServiceOrderId == serviceOrderId)
                .Where(x => x.SlotType == nameof(WorkSlotType.DIAGNOSE))
                .Where(x =>
                    x.Status != nameof(WorkSlotStatus.CANCELLED) &&
                    x.Status != nameof(WorkSlotStatus.NOSHOW) &&
                    x.Status != nameof(WorkSlotStatus.COMPLETED))
                .OrderByDescending(x => x.ActualStartTime ?? x.StartTime)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync();

            if (existingSlot != null && string.Equals(existingSlot.Status, nameof(WorkSlotStatus.IN_PROGRESS), StringComparison.OrdinalIgnoreCase))
            {
                return existingSlot;
            }

            var blockingSlots = await _context.WorkSlots.AsNoTracking()
                .Where(x => x.BayId.HasValue && diagnoseBayIds.Contains(x.BayId.Value))
                .Where(x => x.Id != (existingSlot != null ? existingSlot.Id : 0))
                .Where(x =>
                    x.Status != nameof(WorkSlotStatus.CANCELLED) &&
                    x.Status != nameof(WorkSlotStatus.NOSHOW) &&
                    x.Status != nameof(WorkSlotStatus.COMPLETED) &&
                    x.Status != nameof(WorkSlotStatus.DIAGNOSED_WAITING))
                .Select(x => new
                {
                    BayId = x.BayId!.Value,
                    x.StartTime,
                    x.EndTime
                })
                .ToListAsync();

            var availableByBay = diagnoseBays
                .Select(bay =>
                {
                    var maxEnd = blockingSlots
                        .Where(x => x.BayId == bay.Id)
                        .Select(x => x.EndTime)
                        .DefaultIfEmpty(planningAnchor)
                        .Max();
                    var availableAt = maxEnd > planningAnchor ? maxEnd : planningAnchor;
                    return new
                    {
                        bay.Id,
                        bay.Code,
                        bay.Name,
                        AvailableAt = availableAt,
                        IsPreferred = preferredBayId.HasValue && bay.Id == preferredBayId.Value
                    };
                })
                .OrderBy(x => x.AvailableAt)
                .ThenByDescending(x => x.IsPreferred)
                .ThenBy(x => x.Code ?? x.Name ?? string.Empty)
                .ToList();

            var selectedBay = availableByBay.First();
            var slotStart = selectedBay.AvailableAt;
            var slotEnd = slotStart.AddMinutes(totalMinutes);

            if (existingSlot == null)
            {
                existingSlot = new WorkSlot
                {
                    BranchId = serviceOrder.ExecuteBranchId,
                    BayId = selectedBay.Id,
                    TechnicianId = null,
                    StartTime = slotStart,
                    EndTime = slotEnd,
                    SlotType = nameof(WorkSlotType.DIAGNOSE),
                    CustomerId = serviceOrder.CustomerId,
                    VehicleId = serviceOrder.VehicleId,
                    AppointmentId = serviceOrder.AppointmentId,
                    ServiceOrderId = serviceOrder.Id,
                    IsAdditional = false,
                    SequenceOrder = 1,
                    Status = nameof(WorkSlotStatus.RESERVED),
                    Notes = AutoDiagnosisQueueSlotMarker
                };
                _context.WorkSlots.Add(existingSlot);
            }
            else
            {
                existingSlot.BranchId = serviceOrder.ExecuteBranchId;
                existingSlot.BayId = selectedBay.Id;
                existingSlot.StartTime = slotStart;
                existingSlot.EndTime = slotEnd;
                existingSlot.SlotType = nameof(WorkSlotType.DIAGNOSE);
                existingSlot.CustomerId = serviceOrder.CustomerId;
                existingSlot.VehicleId = serviceOrder.VehicleId;
                existingSlot.AppointmentId = serviceOrder.AppointmentId;
                existingSlot.ServiceOrderId = serviceOrder.Id;
                existingSlot.IsAdditional = false;
                existingSlot.SequenceOrder = 1;
                if (string.IsNullOrWhiteSpace(existingSlot.Status) ||
                    string.Equals(existingSlot.Status, nameof(WorkSlotStatus.CANCELLED), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(existingSlot.Status, nameof(WorkSlotStatus.NOSHOW), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(existingSlot.Status, nameof(WorkSlotStatus.COMPLETED), StringComparison.OrdinalIgnoreCase))
                {
                    existingSlot.Status = nameof(WorkSlotStatus.RESERVED);
                }

                existingSlot.Notes = AppendDiagnosisSlotMarker(existingSlot.Notes, AutoDiagnosisQueueSlotMarker);
                _context.WorkSlots.Update(existingSlot);
            }

            await _context.SaveChangesAsync();
            return existingSlot;
        }

        private static string AppendDiagnosisSlotMarker(string? notes, string marker)
        {
            if (string.IsNullOrWhiteSpace(marker))
            {
                return notes ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(notes))
            {
                return marker;
            }

            return notes.Contains(marker, StringComparison.OrdinalIgnoreCase)
                ? notes
                : $"{notes} {marker}";
        }

        private List<string> ResolveConfiguredDiagnoseBayCodes(long branchId)
        {
            var section = _configuration.GetSection("DiagnosisQueueScheduling:BranchDiagnoseBayCodes");
            if (!section.Exists())
            {
                return new List<string>();
            }

            var branchSection = section.GetSection(branchId.ToString());
            if (!branchSection.Exists())
            {
                return new List<string>();
            }

            var rawCodes = branchSection.Get<string[]>() ?? Array.Empty<string>();
            return rawCodes
                .Select(x => (x ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();
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

            return dispatchTags.Any(tag => DispatchTagMatchesSpecialty(tag, teamSpecialtyTag));
        }

        public async Task<(bool IsCompatible, string Reason)> ValidateTeamBayCompatibilityAsync(long serviceOrderId, long? teamId, long bayId)
        {
            if (!teamId.HasValue || teamId.Value <= 0 || bayId <= 0)
            {
                return (true, "OK");
            }

            var team = await _context.Teams.AsNoTracking()
                .Where(x => x.Id == teamId.Value && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Code,
                    x.BranchId,
                    x.SpecialtyTag
                })
                .FirstOrDefaultAsync();

            if (team == null)
            {
                return (false, $"Khong tim thay to #{teamId.Value}.");
            }

            var bay = await _context.Bays.AsNoTracking()
                .Where(x => x.Id == bayId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.BranchId,
                    x.BayType
                })
                .FirstOrDefaultAsync();

            if (bay == null)
            {
                return (false, $"Khong tim thay khoang #{bayId}.");
            }

            if (bay.BranchId != team.BranchId)
            {
                return (false, "Khoang va to khong thuoc cung chi nhanh.");
            }

            var normalizedBayType = NormalizeToken(bay.BayType);
            if (string.IsNullOrWhiteSpace(normalizedBayType))
            {
                return (true, "OK");
            }

            if (normalizedBayType == BayTypeSpDiagnose)
            {
                return (true, "OK");
            }

            var allowedBayTypes = ResolveBaseBayTypesForTeam(team.Code, team.SpecialtyTag);
            if (allowedBayTypes.Contains(normalizedBayType, StringComparer.OrdinalIgnoreCase))
            {
                return (true, "OK");
            }

            if (string.Equals(NormalizeToken(team.Code), TeamCodeFastMaint, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(normalizedBayType, BayTypeGrLift, StringComparison.OrdinalIgnoreCase))
            {
                var allowOverflow = await CanQuickTeamOverflowToGeneralRepairAsync(serviceOrderId, team.BranchId);
                if (allowOverflow)
                {
                    return (true, "OK");
                }
            }

            return (false, $"To '{team.Name}' khong duoc phep vao khoang {normalizedBayType} neu chua co phe duyet dieu chuyen.");
        }

        private static bool DispatchTagMatchesTeam(string dispatchTag, string? specialtyTag, string? teamCode)
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

        private static bool DispatchTagMatchesSpecialty(string dispatchTag, string specialtyTag)
        {
            var dispatch = NormalizeDispatchTag(dispatchTag);
            var specialty = NormalizeDispatchTag(specialtyTag);
            if (dispatch == null || specialty == null)
            {
                return true;
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

        private static IReadOnlyList<string> ResolveBaseBayTypesForTeam(string? teamCode, string? specialtyTag)
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

        private async Task<bool> CanQuickTeamOverflowToGeneralRepairAsync(long serviceOrderId, long branchId)
        {
            if (branchId <= 0)
            {
                return false;
            }

            var orderCategory = NormalizeToken(await _context.ServiceOrders.AsNoTracking()
                .Where(x => x.Id == serviceOrderId)
                .Select(x => x.OrderCategory)
                .FirstOrDefaultAsync());

            if (!QuickOrderCategories.Contains(orderCategory, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            var bayRows = await _context.Bays.AsNoTracking()
                .Where(x => x.BranchId == branchId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
                .Where(x => x.IsActive == true)
                .Select(x => new
                {
                    x.BayType,
                    x.CurrentOccupiedVehicleId,
                    x.OperationalStatus
                })
                .ToListAsync();

            if (bayRows.Count == 0)
            {
                return false;
            }

            var quickRows = bayRows
                .Where(x =>
                    string.Equals(NormalizeToken(x.BayType), BayTypeQsLift, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeToken(x.BayType), BayTypeQsFlat, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var repairRows = bayRows
                .Where(x =>
                    string.Equals(NormalizeToken(x.BayType), BayTypeGrLift, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeToken(x.BayType), BayTypeGrAlign, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(NormalizeToken(x.BayType), BayTypeGrElectrical, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (quickRows.Count == 0 || repairRows.Count == 0)
            {
                return false;
            }

            var quickBusy = quickRows.Count(x => x.CurrentOccupiedVehicleId.HasValue || !IsOperationalBayAvailable(x.OperationalStatus));
            var repairBusy = repairRows.Count(x => x.CurrentOccupiedVehicleId.HasValue || !IsOperationalBayAvailable(x.OperationalStatus));

            var quickFull = quickBusy >= quickRows.Count;
            var repairFreeRatio = (repairRows.Count - repairBusy) / (double)repairRows.Count;
            return quickFull && repairFreeRatio > 0.5d;
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
                        (string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == StaffStatusActive));

                if (leaderIsValid)
                {
                    return teamLeaderId.Value;
                }
            }

            var firstTeamTechnicianId = await _context.Staffs.AsNoTracking()
                .Where(x => x.TeamId == teamId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == StaffStatusActive)
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
            bool managerOverride,
            long? assignedByTeamLeaderId = null)
        {
            var now = VietnamTime.Now;
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
                diagnosis.AssignedByTeamLeaderId = assignedByTeamLeaderId;
                diagnosis.AssignedAt ??= now;
                diagnosis.StartedAt = null;

                diagnosis.ApprovedByForemanId = null;
                diagnosis.ApprovedByForemanAt = null;
                diagnosis.ApprovedByTeamLeaderAt = null;
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
            var now = VietnamTime.Now;
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

            var serviceOrderIds = list
                .Select(x => x.ServiceOrderId)
                .Distinct()
                .ToList();
            if (serviceOrderIds.Count > 0)
            {
                var diagnoseSlots = await _context.WorkSlots
                    .Where(s =>
                        s.ServiceOrderId.HasValue &&
                        serviceOrderIds.Contains(s.ServiceOrderId.Value) &&
                        (s.SlotType == nameof(WorkSlotType.DIAGNOSE) || s.SequenceOrder <= 1) &&
                        (s.Status == nameof(WorkSlotStatus.ARRIVED) ||
                         s.Status == nameof(WorkSlotStatus.RESERVED) ||
                         s.Status == nameof(WorkSlotStatus.LOCKED)))
                    .ToListAsync();

                foreach (var slot in diagnoseSlots)
                {
                    if (bayId.HasValue && bayId.Value > 0)
                    {
                        slot.BayId = bayId.Value;
                    }

                    slot.Status = nameof(WorkSlotStatus.IN_PROGRESS);
                    slot.ActualStartTime ??= now;
                    slot.TechnicianId ??= technicianId;
                }
            }

            _context.Diagnoses.UpdateRange(list);
            await _context.SaveChangesAsync();
        }

        public async Task<DiagnosisDispatchSuggestionDto> BuildDispatchSuggestionAsync(long serviceOrderId, long teamId)
        {
            var team = await _context.Teams.AsNoTracking()
                .Where(x => x.Id == teamId && (!x.IsDeleted.HasValue || x.IsDeleted.Value == false))
                .Select(x => new { x.Id, x.Name, x.Code, x.SpecialtyTag, x.BranchId })
                .FirstOrDefaultAsync();

            if (team == null)
            {
                throw new InvalidOperationException("Khong tim thay to ky thuat duoc chon.");
            }

            var serviceOrder = await _context.ServiceOrders.AsNoTracking()
                .Where(x => x.Id == serviceOrderId)
                .Select(x => new
                {
                    x.Id,
                    x.ExecuteBranchId,
                    x.OrderCategory,
                    x.Status
                })
                .FirstOrDefaultAsync();

            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Khong tim thay lenh sua chua.");
            }

            var technicians = await _context.Staffs.AsNoTracking()
                .Where(x => x.TeamId == teamId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => string.IsNullOrEmpty(x.Status) || x.Status.ToUpper() == StaffStatusActive)
                .Select(x => new { x.Id, x.FullName, x.SkillLevel })
                .ToListAsync();

            var technicianIds = technicians.Select(x => x.Id).Distinct().ToList();
            var loads = new Dictionary<long, int>();

            if (technicianIds.Count > 0)
            {
                var loadRows = await _context.Diagnoses.AsNoTracking()
                    .Where(x => x.Status == DiagnosisStatusAssigned)
                    .Where(x =>
                        (x.TechnicianId.HasValue && technicianIds.Contains(x.TechnicianId.Value)) ||
                        (x.AssistantTechnicianId.HasValue && technicianIds.Contains(x.AssistantTechnicianId.Value)))
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
                    if (row.TechnicianId.HasValue && loadBuckets.TryGetValue(row.TechnicianId.Value, out var leadOrders))
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

            var serviceSignals = await _context.ServiceOrderDetails.AsNoTracking()
                .Where(x => x.ServiceOrderId == serviceOrderId && x.StandardServiceId.HasValue)
                .Join(
                    _context.StandardServices.AsNoTracking(),
                    detail => detail.StandardServiceId!.Value,
                    standard => standard.Id,
                    (detail, standard) => new
                    {
                        standard.Code,
                        standard.Name
                    })
                .ToListAsync();

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

            var normalizedOrderStatus = NormalizeToken(serviceOrder.Status);
            var normalizedOrderCategory = NormalizeToken(serviceOrder.OrderCategory);
            var normalizedTeamCode = NormalizeToken(team.Code);

            var preferredBayTypes = new List<string>();

            if (DiagnoseFirstStatuses.Contains(normalizedOrderStatus, StringComparer.OrdinalIgnoreCase))
            {
                preferredBayTypes.Add(BayTypeSpDiagnose);
            }

            switch (normalizedOrderCategory)
            {
                case "MAINTENANCE":
                case "QUICK_SERVICE":
                    preferredBayTypes.Add(BayTypeQsLift);
                    preferredBayTypes.Add(BayTypeQsFlat);
                    if (await CanQuickTeamOverflowToGeneralRepairAsync(serviceOrderId, team.BranchId))
                    {
                        preferredBayTypes.Add(BayTypeGrLift);
                    }
                    break;

                case "GENERAL_REPAIR":
                    if (needsElectrical)
                    {
                        preferredBayTypes.Add(BayTypeGrElectrical);
                    }
                    preferredBayTypes.Add(BayTypeGrLift);
                    if (needsAlignment)
                    {
                        preferredBayTypes.Add(BayTypeGrAlign);
                    }
                    break;

                case "INSURANCE_REPAIR":
                    preferredBayTypes.Add(BayTypeSpBodyPaint);
                    break;

                case "REWORK":
                case "WARRANTLY":
                case "WARRANTY":
                    preferredBayTypes.Add(BayTypeGrLift);
                    if (needsAlignment)
                    {
                        preferredBayTypes.Add(BayTypeGrAlign);
                    }
                    if (needsElectrical)
                    {
                        preferredBayTypes.Add(BayTypeGrElectrical);
                    }
                    break;
            }

            if (needsDetailing || string.Equals(normalizedTeamCode, TeamCodeCarCare, StringComparison.OrdinalIgnoreCase))
            {
                preferredBayTypes.Add(BayTypeSpDetailing);
            }

            preferredBayTypes.AddRange(ResolveBaseBayTypesForTeam(team.Code, team.SpecialtyTag));
            preferredBayTypes.Add(BayTypeSpDiagnose);
            preferredBayTypes = preferredBayTypes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeToken)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var preferredBayTypeOrder = preferredBayTypes
                .Select((value, index) => new { value, index })
                .ToDictionary(x => x.value, x => x.index, StringComparer.OrdinalIgnoreCase);

            var suggestedBay = await _context.Bays.AsNoTracking()
                .Where(x => x.BranchId == team.BranchId)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => x.IsActive == true)
                .OrderBy(x => x.CurrentOccupiedVehicleId.HasValue ? 1 : 0)
                .ThenBy(x => x.Code)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.BayType,
                    x.CurrentOccupiedVehicleId,
                    x.OperationalStatus
                })
                .ToListAsync();

            var freeBay = suggestedBay
                .Where(x => !x.CurrentOccupiedVehicleId.HasValue && IsOperationalBayAvailable(x.OperationalStatus))
                .OrderBy(x =>
                {
                    var bayType = NormalizeToken(x.BayType);
                    return preferredBayTypeOrder.TryGetValue(bayType, out var order) ? order : int.MaxValue;
                })
                .ThenBy(x => x.Code ?? x.Name ?? string.Empty)
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
            var now = VietnamTime.Now;
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

            long? primaryTeamId = existingMeta?.PrimaryTeamId;
            if (!primaryTeamId.HasValue || primaryTeamId.Value <= 0)
            {
                primaryTeamId = list
                    .Where(x => x.AssignedTeamId.HasValue && x.AssignedTeamId.Value > 0)
                    .Select(x => x.AssignedTeamId)
                    .FirstOrDefault();
            }
            if (!primaryTeamId.HasValue || primaryTeamId.Value <= 0)
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

            var now = VietnamTime.Now;
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
            var now = VietnamTime.Now;
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
            var now = VietnamTime.Now;
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
                    diagnosis.ApprovedByForemanAt = null;
                    diagnosis.ApprovedByTeamLeaderAt = null;
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
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                diagnosis.ApprovedByForemanId = teamLeaderId;
                diagnosis.ApprovedByForemanAt = now;
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

        public async Task ApproveFinalAsync(IEnumerable<Diagnosis> diagnoses, long teamLeaderId, string? note)
        {
            var now = VietnamTime.Now;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            foreach (var diagnosis in list)
            {
                diagnosis.ApprovedByTeamLeaderAt = now;
                diagnosis.FinalApprovedAt = now;
                diagnosis.FinalApprovalNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                diagnosis.UpdatedBy = teamLeaderId;
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

            serviceOrder.UpdatedAt = VietnamTime.Now;
            _context.ServiceOrders.Update(serviceOrder);
            await _context.SaveChangesAsync();
        }

        public async Task<Diagnosis?> GetDiagnosisEntityAsync(long id)
        {
            return await _context.Diagnoses.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task CompleteDiagnosisAsync(Diagnosis diagnosis)
        {
            var now = VietnamTime.Now;
            diagnosis.Status = DiagnosisStatusCompleted;
            diagnosis.DiagnosedAt = now;
            diagnosis.CancellationStatus = CancellationStatusNone;
            diagnosis.UpdatedAt = now;
            _context.Diagnoses.Update(diagnosis);
            await _context.SaveChangesAsync();

            var allStatuses = await _context.Diagnoses
                .Where(x => x.ServiceOrderId == diagnosis.ServiceOrderId)
                .Select(x => x.Status)
                .ToListAsync();
            var isAllCompleted = allStatuses.Count > 0 &&
                                 allStatuses.All(s => s == DiagnosisStatusCompleted);
            if (isAllCompleted)
            {
                var diagnoseSlots = await _context.WorkSlots
                    .Where(s =>
                        s.ServiceOrderId == diagnosis.ServiceOrderId &&
                        (s.SlotType == nameof(WorkSlotType.DIAGNOSE) || s.SequenceOrder <= 1) &&
                        s.Status != nameof(WorkSlotStatus.CANCELLED) &&
                        s.Status != nameof(WorkSlotStatus.NOSHOW) &&
                        s.Status != nameof(WorkSlotStatus.COMPLETED))
                    .ToListAsync();

                foreach (var slot in diagnoseSlots)
                {
                    slot.Status = nameof(WorkSlotStatus.DIAGNOSED_WAITING);
                    slot.SlotType = nameof(WorkSlotType.BUFFER);
                    slot.BayId = null;
                    slot.ActualReleaseTime ??= now;
                    slot.EndTime = now > slot.StartTime ? now : slot.EndTime;

                    if (string.IsNullOrWhiteSpace(slot.Notes))
                    {
                        slot.Notes = "[DIAGNOSED_WAITING_RELEASED]";
                    }
                    else if (!slot.Notes.Contains("[DIAGNOSED_WAITING_RELEASED]", StringComparison.OrdinalIgnoreCase))
                    {
                        slot.Notes += " [DIAGNOSED_WAITING_RELEASED]";
                    }
                }
            }

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

        private static string NormalizeToken(string? rawValue)
        {
            return (rawValue ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Replace("-", "_")
                .Replace(" ", "_");
        }

        private static bool IsCancellationPending(Diagnosis diagnosis)
        {
            return diagnosis.CancellationRequestedAt.HasValue && !diagnosis.CancellationDecisionAt.HasValue;
        }

        private static bool IsPendingLikeStatus(string? status)
        {
            return status == DiagnosisStatusPending;
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
