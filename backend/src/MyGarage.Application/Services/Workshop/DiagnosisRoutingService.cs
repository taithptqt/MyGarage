using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    public partial class DiagnosisRoutingService : IDiagnosisRoutingService
    {
        private readonly IDiagnosisStore _store;

        public DiagnosisRoutingService(IDiagnosisStore store)
        {
            _store = store;
        }

        public async Task<WorkSlot?> ScheduleDiagnosisQueueSlotAsync(long serviceOrderId, int diagnoseMinutes, int bufferMinutes = 15, long? preferredBayId = null)
        {
            if (serviceOrderId <= 0)
            {
                return null;
            }

            var serviceOrder = await _store.GetQueueServiceOrderInfoAsync(serviceOrderId);
            if (serviceOrder == null || serviceOrder.ExecuteBranchId <= 0)
            {
                return null;
            }

            var effectiveDiagnoseMinutes = Math.Max(5, diagnoseMinutes > 0 ? diagnoseMinutes : DiagnosisQueueDefaultMinutes);
            var effectiveBufferMinutes = Math.Max(0, bufferMinutes > 0 ? bufferMinutes : DiagnosisQueueDefaultBufferMinutes);
            var totalMinutes = effectiveDiagnoseMinutes + effectiveBufferMinutes;
            var planningAnchor = VietnamTime.Now;

            var configuredDiagnoseBayCodes = _store.GetConfiguredDiagnoseBayCodes(serviceOrder.ExecuteBranchId).ToList();
            if (configuredDiagnoseBayCodes.Count == 0)
            {
                return null;
            }

            var diagnoseBays = (await _store.GetActiveDiagnoseBaysAsync(serviceOrder.ExecuteBranchId, BayTypeSpDiagnose)).ToList();
            if (diagnoseBays.Count == 0)
            {
                return null;
            }

            diagnoseBays = diagnoseBays
                .Where(x => configuredDiagnoseBayCodes.Contains((x.Code ?? string.Empty).Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase))
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
            var existingSlot = await _store.GetLatestActiveDiagnoseWorkSlotAsync(serviceOrderId);

            if (existingSlot != null && string.Equals(existingSlot.Status, nameof(WorkSlotStatus.IN_PROGRESS), StringComparison.OrdinalIgnoreCase))
            {
                return existingSlot;
            }

            var blockingSlots = await _store.GetBlockingWorkSlotsForBaysAsync(
                diagnoseBayIds,
                existingSlot != null ? existingSlot.Id : 0);

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
                _store.AddWorkSlot(existingSlot);
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
                    string.Equals(existingSlot.Status, nameof(WorkSlotStatus.CANCELED), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(existingSlot.Status, nameof(WorkSlotStatus.NOSHOW), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(existingSlot.Status, nameof(WorkSlotStatus.COMPLETED), StringComparison.OrdinalIgnoreCase))
                {
                    existingSlot.Status = nameof(WorkSlotStatus.RESERVED);
                }

                existingSlot.Notes = AppendDiagnosisSlotMarker(existingSlot.Notes, AutoDiagnosisQueueSlotMarker);
                _store.UpdateWorkSlot(existingSlot);
            }

            await _store.SaveChangesInTransactionAsync();
            return existingSlot;
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

            var teamSpecialtyTag = NormalizeDispatchTag(await _store.GetTeamSpecialtyTagAsync(teamId.Value));
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

            var team = await _store.GetDispatchTeamByIdAsync(teamId.Value);
            if (team == null)
            {
                return (false, $"Không tìm thấy tổ #{teamId.Value}.");
            }

            var bay = await _store.GetBayDetailByIdAsync(bayId);
            if (bay == null)
            {
                return (false, $"Không tìm thấy khoang #{bayId}.");
            }

            if (bay.BranchId != team.BranchId)
            {
                return (false, "Khoang và tổ không thuộc cùng chi nhánh.");
            }

            var normalizedBayType = NormalizeToken(bay.BayType);
            if (string.IsNullOrWhiteSpace(normalizedBayType) || normalizedBayType == BayTypeSpDiagnose)
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
                var allowOverflow = await CanQuickTeamOverflowToGeneralRepairAsync(serviceOrderId, team.BranchId ?? 0);
                if (allowOverflow)
                {
                    return (true, "OK");
                }
            }

            return (false, $"Tổ '{team.Name}' không được phép vào khoang {normalizedBayType} nếu chưa có phê duyệt điều chuyển.");
        }

        public Task<bool> IsBayInBranchAsync(long bayId, long branchId)
        {
            return _store.IsBayInBranchAsync(bayId, branchId);
        }

        public async Task<long?> ResolveDiagnoseBayIdForBranchAsync(long branchId, long? preferredBayId = null)
        {
            if (branchId <= 0)
            {
                return null;
            }

            var configuredDiagnoseBayCodes = _store.GetConfiguredDiagnoseBayCodes(branchId).ToList();
            var hasConfiguredCodes = configuredDiagnoseBayCodes.Count > 0;
            if (!hasConfiguredCodes)
            {
                return null;
            }

            long? validatedPreferredBayId = null;
            if (preferredBayId.HasValue && preferredBayId.Value > 0)
            {
                validatedPreferredBayId = await _store.FindValidConfiguredBayIdAsync(
                    preferredBayId.Value,
                    branchId,
                    hasConfiguredCodes,
                    configuredDiagnoseBayCodes);
            }

            var branchBays = await _store.GetActiveBranchBaysAsync(branchId);

            var diagnoseBays = branchBays
                .Where(x => string.Equals(NormalizeToken(x.BayType), BayTypeSpDiagnose, StringComparison.OrdinalIgnoreCase))
                .Where(x =>
                {
                    var code = (x.Code ?? string.Empty).Trim().ToUpperInvariant();
                    return configuredDiagnoseBayCodes.Contains(code, StringComparer.OrdinalIgnoreCase);
                })
                .OrderBy(x =>
                {
                    var code = (x.Code ?? string.Empty).Trim().ToUpperInvariant();
                    var index = configuredDiagnoseBayCodes.IndexOf(code);
                    return index >= 0 ? index : int.MaxValue;
                })
                .ThenBy(x => x.Code ?? x.Name ?? string.Empty)
                .Take(2)
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

        public async Task<long?> ResolveDefaultTechnicianForTeamAsync(long teamId)
        {
            var teamLeaderId = await _store.GetTeamLeaderIdAsync(teamId);

            if (teamLeaderId.HasValue)
            {
                var leaderIsValid = await _store.IsActiveTeamStaffAsync(teamLeaderId.Value, teamId, StaffStatusActive);
                if (leaderIsValid)
                {
                    return teamLeaderId.Value;
                }
            }

            var firstTeamTechnicianId = await _store.GetFirstActiveTeamStaffIdAsync(teamId, StaffStatusActive) ?? 0;
            return firstTeamTechnicianId > 0 ? firstTeamTechnicianId : null;
        }

        public async Task<bool> CanQuickTeamOverflowToGeneralRepairAsync(long serviceOrderId, long branchId)
        {
            if (branchId <= 0)
            {
                return false;
            }

            var orderCategory = NormalizeToken(await _store.GetServiceOrderCategoryAsync(serviceOrderId));
            if (!QuickOrderCategories.Contains(orderCategory, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            var bayRows = await _store.GetActiveBranchBayAvailabilityAsync(branchId);
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
    }
}
