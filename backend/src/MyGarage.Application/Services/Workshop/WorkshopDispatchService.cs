using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public sealed class WorkshopDispatchService : IWorkshopDispatchService
    {
        private static readonly HashSet<string> ActiveOrderStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "CHECKED_IN",
            "WAITING",
            "APPROVED",
            "WAITING_PARTS",
            "IN_PROGRESS",
            "REPAIRING",
            "READY"
        };

        private readonly IWorkshopDispatchStore _store;

        public WorkshopDispatchService(IWorkshopDispatchStore store)
        {
            _store = store;
        }

        public async Task<WorkshopDispatchBoardDto> GetBoardAsync(long branchId, DateTime date)
        {
            var day = date.Date;
            var dayEnd = day.AddDays(1);

            var serviceOrders = await _store.GetDispatchServiceOrdersAsync(branchId, day, dayEnd, ActiveOrderStatuses);

            var serviceOrderIds = serviceOrders.Select(x => x.Id).ToList();
            if (serviceOrderIds.Count == 0)
            {
                return new WorkshopDispatchBoardDto
                {
                    BranchId = branchId,
                    Date = day
                };
            }

            var allocations = await _store.GetBayAllocationsByServiceOrderIdsAsync(serviceOrderIds);
            var details = await _store.GetServiceOrderDetailsByServiceOrderIdsAsync(serviceOrderIds);

            var detailIds = details.Select(x => x.Id).ToList();
            var assignments = await _store.GetAssignmentsByDetailIdsAsync(detailIds);

            var technicianIds = assignments.Select(x => x.TechnicianId).Distinct().ToList();
            var technicians = await _store.GetTechniciansByIdsAsync(technicianIds);

            var materialRequests = await _store.GetMaterialRequestsByServiceOrderIdsAsync(serviceOrderIds);
            var materialRequestIds = materialRequests.Select(x => x.Id).ToList();
            var materialRequestDetails = await _store.GetMaterialRequestDetailsByRequestIdsAsync(materialRequestIds);

            var materialRequestsByServiceOrderId = materialRequests
                .Where(x => x.ServiceOrderId.HasValue)
                .GroupBy(x => x.ServiceOrderId!.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

            var materialDetailsByRequestId = materialRequestDetails
                .GroupBy(x => x.RequestId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var detailsByServiceOrderId = details
                .GroupBy(x => x.ServiceOrderId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var assignmentsByDetailId = assignments
                .GroupBy(x => x.ServiceOrderDetailId)
                .ToDictionary(x => x.Key, x => x.ToList());

            var latestAllocationByServiceOrderId = allocations
                .GroupBy(x => x.ServiceOrderId)
                .ToDictionary(x => x.Key, x => x.FirstOrDefault());

            var cards = new List<WorkshopDispatchCardDto>(serviceOrders.Count);
            foreach (var serviceOrder in serviceOrders)
            {
                detailsByServiceOrderId.TryGetValue(serviceOrder.Id, out var orderDetails);
                orderDetails ??= new List<Models.ServiceOrderDetail>();

                latestAllocationByServiceOrderId.TryGetValue(serviceOrder.Id, out var latestAllocation);

                var laborLines = orderDetails.Count(x => NormalizeServiceOrderDetailItemType(x.ItemType) == "LABOR");
                var partLines = orderDetails.Count(x => NormalizeServiceOrderDetailItemType(x.ItemType) == "PART");

                materialRequestsByServiceOrderId.TryGetValue(serviceOrder.Id, out var requestRows);
                requestRows ??= new List<Models.MaterialRequest>();
                var requestIdSet = requestRows.Select(x => x.Id).ToHashSet();
                var relatedMaterialDetails = materialDetailsByRequestId
                    .Where(x => requestIdSet.Contains(x.Key))
                    .SelectMany(x => x.Value)
                    .ToList();

                var pendingLines = relatedMaterialDetails.Count(x =>
                    string.IsNullOrWhiteSpace(x.Status) ||
                    string.Equals(x.Status, "PENDING", StringComparison.OrdinalIgnoreCase));
                var issuedLines = relatedMaterialDetails.Count(x =>
                    (x.IssuedQuantity ?? 0m) > 0m ||
                    string.Equals(x.Status, "PARTIAL", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Status, "COMPLETED", StringComparison.OrdinalIgnoreCase));

                var detailDtos = new List<WorkshopDispatchDetailDto>(orderDetails.Count);
                foreach (var detail in orderDetails)
                {
                    assignmentsByDetailId.TryGetValue(detail.Id, out var detailAssignments);
                    detailAssignments ??= new List<Models.ServiceStaffAssignment>();

                    var assignmentDtos = detailAssignments
                        .Select(x =>
                        {
                            technicians.TryGetValue(x.TechnicianId, out var tech);
                            return new WorkshopDispatchAssignmentDto
                            {
                                AssignmentId = x.Id,
                                TechnicianId = x.TechnicianId,
                                TechnicianName = tech?.FullName ?? x.TechnicianId.ToString(CultureInfo.InvariantCulture),
                                TechnicianSpecialty = tech?.Specialty,
                                RoleInTask = x.RoleInTask ?? string.Empty,
                                SplitRatio = x.SplitRatio,
                                Status = x.Status ?? string.Empty,
                                ActualStartTime = x.ActualStartTime,
                                ActualEndTime = x.ActualEndTime
                            };
                        })
                        .OrderBy(x => x.RoleInTask)
                        .ThenBy(x => x.TechnicianName, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    detailDtos.Add(new WorkshopDispatchDetailDto
                    {
                        ServiceOrderDetailId = detail.Id,
                        ItemName = detail.ItemName,
                        ItemType = NormalizeServiceOrderDetailItemType(detail.ItemType),
                        Quantity = detail.Quantity,
                        RequiredSpecialty = ResolveRequiredSpecialty(detail.ItemName, detail.Description),
                        Assignments = assignmentDtos
                    });
                }

                var allAssignments = detailDtos.SelectMany(x => x.Assignments).ToList();
                cards.Add(new WorkshopDispatchCardDto
                {
                    ServiceOrderId = serviceOrder.Id,
                    ServiceOrderCode = serviceOrder.Code,
                    ServiceOrderStatus = serviceOrder.Status,
                    VehicleId = serviceOrder.VehicleId,
                    LicensePlate = serviceOrder.Vehicle?.LicensePlate ?? "-",
                    CustomerName = serviceOrder.Customer?.FullName ?? "-",
                    BayId = latestAllocation?.BayId,
                    BayName = latestAllocation?.Bay?.Name,
                    BayStatus = latestAllocation?.Bay?.OperationalStatus,
                    BayAllocatedAt = latestAllocation?.AllocatedAt,
                    TotalLaborLines = laborLines,
                    TotalPartLines = partLines,
                    AssignmentCount = allAssignments.Count,
                    DistinctTechnicianCount = allAssignments.Select(x => x.TechnicianId).Distinct().Count(),
                    MaterialRequestCount = requestRows.Count,
                    MaterialRequestPendingLines = pendingLines,
                    MaterialRequestIssuedLines = issuedLines,
                    ReadyForStorekeeper = partLines > 0 && requestRows.Count > 0,
                    Details = detailDtos
                });
            }

            return new WorkshopDispatchBoardDto
            {
                BranchId = branchId,
                Date = day,
                ServiceOrders = cards
                    .OrderByDescending(x => x.BayAllocatedAt ?? DateTime.MinValue)
                    .ThenBy(x => x.ServiceOrderCode, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }

        private static string NormalizeServiceOrderDetailItemType(string? itemType)
        {
            var token = NormalizeToken(itemType);
            return token switch
            {
                "PART" or "PARTS" or "PRODUCT" or "MATERIAL" or "INVENTORY" => "PART",
                "SERVICE" or "STANDARD_SERVICE" or "LABOR" => "LABOR",
                _ => "LABOR"
            };
        }

        private static string ResolveRequiredSpecialty(string? itemName, string? description)
        {
            var source = CompactSearchText($"{itemName} {description}");
            if (ContainsAny(source, "SON", "DONG", "VO", "PAINT", "BODY"))
            {
                return "PAINTING";
            }

            if (ContainsAny(source, "DIEN", "ELECTRIC", "ECU", "CAMBIEN", "SENSOR", "MAYLANH", "AC"))
            {
                return "ELECTRICAL";
            }

            if (ContainsAny(source, "GAM", "CHASSIS", "PHANH", "TREO", "LAI", "BANH", "LOP"))
            {
                return "CHASSIS";
            }

            if (ContainsAny(source, "MAY", "ENGINE", "DONGCO", "XECMANG", "HOPSO"))
            {
                return "ENGINE";
            }

            return "GENERAL";
        }

        private static string NormalizeToken(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToUpperInvariant().Replace("-", "_").Replace(" ", "_");
        }

        private static string CompactSearchText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToUpperInvariant(character));
                }
            }

            return builder
                .ToString()
                .Replace('Đ', 'D')
                .Replace('đ', 'D')
                .Normalize(NormalizationForm.FormC);
        }

        private static bool ContainsAny(string source, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            foreach (var token in tokens)
            {
                var normalized = CompactSearchText(token);
                if (source.Contains(normalized, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
