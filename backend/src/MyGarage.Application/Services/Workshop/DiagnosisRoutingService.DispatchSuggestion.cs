using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;
using static MyGarage.Services.DiagnosisRoutingHelpers;

namespace MyGarage.Services
{
    public partial class DiagnosisRoutingService
    {
        public async Task<DiagnosisDispatchSuggestionDto> BuildDispatchSuggestionAsync(long serviceOrderId, long teamId)
        {
            var team = await _store.GetDispatchTeamByIdAsync(teamId);
            if (team == null)
            {
                throw new InvalidOperationException("Không tìm thấy tổ kỹ thuật được chọn.");
            }

            var serviceOrder = await _store.GetDispatchServiceOrderInfoAsync(serviceOrderId);
            if (serviceOrder == null)
            {
                throw new InvalidOperationException("Không tìm thấy lệnh sửa chữa.");
            }

            var technicians = await _store.GetActiveTeamTechniciansAsync(teamId, StaffStatusActive);
            var technicianIds = technicians.Select(x => x.Id).Distinct().ToList();
            var loads = new Dictionary<long, int>();

            if (technicianIds.Count > 0)
            {
                var assignmentRows = await _store.GetAssignedDiagnosisLoadRowsAsync(technicianIds, DiagnosisStatusAssigned);

                var loadRows = assignmentRows
                    .GroupBy(x => x.DiagnosisId)
                    .Select(group => group
                        .OrderByDescending(x => x.CreatedAt)
                        .ThenByDescending(x => x.Id)
                        .First())
                    .ToList();

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

            var serviceSignals = await _store.GetServiceSignalsForOrderAsync(serviceOrderId);

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
                    if (await CanQuickTeamOverflowToGeneralRepairAsync(serviceOrderId, team.BranchId ?? 0))
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

            var suggestedBay = (await _store.GetActiveBranchBaysAsync(team.BranchId.GetValueOrDefault()))
                .OrderBy(x => x.CurrentOccupiedVehicleId.HasValue ? 1 : 0)
                .ThenBy(x => x.Code)
                .ToList();

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
    }
}
