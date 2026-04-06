using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class DiagnosisService
    {
        private const string DiagnosisStatusPending = "PENDING";
        private const string DiagnosisStatusAssigned = "ASSIGNED";
        private const string DiagnosisStatusCompleted = "COMPLETED";

        private const string CancellationStatusNone = "NONE";
        private const string CancellationStatusPending = "PENDING";
        private const string CancellationStatusApproved = "APPROVED";
        private const string CancellationStatusRejected = "REJECTED";

        private const string ServiceOrderStatusInspecting = "INSPECTING";
        private const string ServiceOrderStatusNew = "NEW";
        private const string ServiceOrderStatusWaitingApproval = "WAITING_APPROVAL";

        private const int UnclaimedEscalationMinutes = 10;
        private const int GoldenCancelWindowMinutes = 10;
        private const int DiagnosisSlaMinutes = 45;

        private static string? NormalizeDispatchTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
            {
                return null;
            }

            return rawTag.Trim().ToUpperInvariant();
        }

        public async Task<long> ResolvePlaceholderTechnicianIdAsync(ServiceOrder serviceOrder, string? dispatchTag = null)
        {
            var normalizedDispatchTag = NormalizeDispatchTag(dispatchTag);

            var leaderByDispatchTag = await _context.Teams.AsNoTracking()
                .Where(x => x.BranchId == serviceOrder.ExecuteBranchId)
                .Where(x => x.LeaderId.HasValue)
                .Where(x => !x.IsDeleted.HasValue || x.IsDeleted.Value == false)
                .Where(x => normalizedDispatchTag == null || x.SpecialtyTag == normalizedDispatchTag)
                .OrderBy(x => x.Id)
                .Select(x => x.LeaderId)
                .FirstOrDefaultAsync();

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
                .Where(x => x.Status == DiagnosisStatusPending || x.Status == DiagnosisStatusAssigned)
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

            var normalizedDispatchTag = NormalizeDispatchTag(dispatchTag);
            if (normalizedDispatchTag == null)
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

            return string.Equals(normalizedDispatchTag, teamSpecialtyTag, StringComparison.OrdinalIgnoreCase);
        }

        public async Task AssignDiagnosesAsync(
            IEnumerable<Diagnosis> diagnoses,
            long technicianId,
            long? assistantTechnicianId,
            long? teamId,
            long? bayId,
            bool managerOverride)
        {
            var now = DateTime.UtcNow;
            var list = diagnoses?.ToList() ?? new List<Diagnosis>();
            if (list.Count == 0)
            {
                return;
            }

            var resolvedTeamId = teamId ?? await ResolveTeamIdForTechnicianAsync(technicianId);

            foreach (var diagnosis in list)
            {
                diagnosis.TechnicianId = technicianId;
                diagnosis.AssistantTechnicianId = assistantTechnicianId;
                diagnosis.Status = DiagnosisStatusAssigned;
                diagnosis.AssignedTeamId = resolvedTeamId;
                diagnosis.AssignedBayId = bayId;
                diagnosis.AssignedAt ??= now;
                diagnosis.StartedAt = null;

                diagnosis.ApprovedByForemanId = null;
                diagnosis.FinalApprovedByManagerId = null;
                diagnosis.FinalApprovedAt = null;
                diagnosis.FinalApprovalNote = null;

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
                .Where(x => string.IsNullOrEmpty(x.Status) || !string.Equals(x.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase))
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
                diagnosis.CancellationStatus = CancellationStatusPending;
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
                if (!string.Equals(diagnosis.CancellationStatus, CancellationStatusPending, StringComparison.OrdinalIgnoreCase))
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
                    diagnosis.CancellationStatus = CancellationStatusApproved;
                }
                else
                {
                    diagnosis.CancellationStatus = CancellationStatusRejected;
                }

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
                order.Status = ServiceOrderStatusWaitingApproval;
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
                "IN_USE" => false,
                "BROKEN" => false,
                "UNDER_MAINTENANCE" => false,
                "MAINTENANCE" => false,
                "SUSPENDED" => false,
                _ => true,
            };
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
