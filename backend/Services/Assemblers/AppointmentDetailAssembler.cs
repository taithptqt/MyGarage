using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Data;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentDetailAssembler : IAppointmentDetailAssembler
    {
        private const decimal InventoryQuantityTolerance = 0.0001m;
        private sealed record PartBomRow(
            long VehicleModelId,
            long ServiceId,
            string? ServiceName,
            long ProductId,
            string? ProductName,
            string? ProductSku,
            decimal Quantity,
            string? Unit);
        private static readonly Regex ProductFallbackLabelRegex = new(
            @"(?:Sản phẩm|San pham|Product)\s*#(?<id>\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex PartShortageItemRegex = new(
            @"^(?<label>.+?)\s*\(cần\s*(?<required>[-+]?\d+(?:[.,]\d+)?),\s*còn\s*(?<available>[-+]?\d+(?:[.,]\d+)?)\s*\)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly IGarageContext _context;

        public AppointmentDetailAssembler(IGarageContext context)
        {
            _context = context;
        }

        public async Task<AppointmentDetailResponseDto> AssembleAsync(Appointment appointment)
        {
            var items = await AssembleManyAsync(new[] { appointment });
            return items.First();
        }

        public async Task<IEnumerable<AppointmentDetailResponseDto>> AssembleManyAsync(IEnumerable<Appointment> appointments)
        {
            var list = appointments?.ToList() ?? new List<Appointment>();
            if (list.Count == 0) return new List<AppointmentDetailResponseDto>();
            var appointmentIds = list.Select(a => a.Id).Distinct().ToList();

            var serviceIds = list
                .SelectMany(a => ParseServiceIds(a.RequestedServiceIds))
                .Distinct()
                .ToList();
            var packageIds = list
                .SelectMany(a => ParseServiceIds(a.RequestedPackageIds))
                .Distinct()
                .ToList();

            var serviceLookup = serviceIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.StandardServices
                    .AsNoTracking()
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name ?? $"#{s.Id}");

            var packageLookup = packageIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Packages
                    .AsNoTracking()
                    .Where(p => packageIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id, p => p.Name ?? $"#{p.Id}");

            var packageItemServiceIdsByPackageId = packageIds.Count == 0
                ? new Dictionary<long, List<long>>()
                : await _context.PackageItems
                    .AsNoTracking()
                    .Where(item =>
                        packageIds.Contains(item.PackageId) &&
                        item.IsDeleted != true &&
                        item.IsActive != false &&
                        item.StandardService.IsDeleted != true &&
                        item.StandardService.IsActive != false)
                    .GroupBy(item => item.PackageId)
                    .ToDictionaryAsync(
                        group => group.Key,
                        group => group.Select(item => item.StandardServiceId).Distinct().ToList());

            var insuranceCompanyIds = list
                .Where(a => a.InsuranceCompanyId.HasValue && a.InsuranceCompanyId.Value > 0)
                .Select(a => a.InsuranceCompanyId!.Value)
                .Distinct()
                .ToList();

            var insuranceCompanyLookup = insuranceCompanyIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.InsuranceCompanies
                    .AsNoTracking()
                    .Where(company => insuranceCompanyIds.Contains(company.Id) && company.IsDeleted != true)
                    .ToDictionaryAsync(company => company.Id, company => company.Name ?? $"#{company.Id}");

            var insuranceAssets = appointmentIds.Count == 0
                ? new List<AppointmentInsuranceAsset>()
                : await _context.AppointmentInsuranceAssets
                    .AsNoTracking()
                    .Where(asset => appointmentIds.Contains(asset.AppointmentId))
                    .OrderBy(asset => asset.SortOrder)
                    .ThenBy(asset => asset.Id)
                    .ToListAsync();

            var insuranceAssetLookup = insuranceAssets
                .GroupBy(asset => asset.AppointmentId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            var flaggedSlots = await _context.WorkSlots
                .AsNoTracking()
                .Where(slot =>
                    slot.AppointmentId.HasValue &&
                    appointmentIds.Contains(slot.AppointmentId.Value) &&
                    slot.Status != nameof(WorkSlotStatus.CANCELED) &&
                    slot.Status != nameof(WorkSlotStatus.NOSHOW) &&
                    slot.Status != nameof(WorkSlotStatus.COMPLETED) &&
                    slot.Notes != null &&
                    (slot.Notes.Contains(WorkSlotNoteMarkers.FlexPendingBayAssignment) ||
                     slot.Notes.Contains(WorkSlotNoteMarkers.PartsShortageAlert)))
                .Select(slot => new
                {
                    AppointmentId = slot.AppointmentId!.Value,
                    slot.Notes
                })
                .ToListAsync();

            var dispatchAlertsByAppointment = flaggedSlots
                .Where(slot => slot.Notes != null && slot.Notes.Contains(WorkSlotNoteMarkers.FlexPendingBayAssignment))
                .GroupBy(slot => slot.AppointmentId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var targetAreas = group
                            .Select(slot => ExtractTargetBayArea(slot.Notes))
                            .Where(area => !string.IsNullOrWhiteSpace(area))
                            .Select(area => area!)
                            .Distinct()
                            .ToList();

                        return BuildDispatchAlertMessage(targetAreas);
                    });

            var rawPartsAlertsByAppointment = flaggedSlots
                .Where(slot => slot.Notes != null && slot.Notes.Contains(WorkSlotNoteMarkers.PartsShortageAlert))
                .GroupBy(slot => slot.AppointmentId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(slot => ExtractPartsShortageDetails(slot.Notes))
                        .Where(detail => !string.IsNullOrWhiteSpace(detail))
                        .Select(detail => detail!)
                        .Distinct()
                        .ToList());

            var shortageProductIds = rawPartsAlertsByAppointment.Values
                .SelectMany(ExtractFallbackProductIds)
                .Distinct()
                .ToList();

            var productLabelById = shortageProductIds.Count == 0
                ? new Dictionary<long, string>()
                : await _context.Products
                    .AsNoTracking()
                    .Where(product => shortageProductIds.Contains(product.Id))
                    .Select(product => new { product.Id, product.Name, product.Sku })
                    .ToDictionaryAsync(
                        product => product.Id,
                        product => ResolvePartLabel(product.Id, product.Name, product.Sku));

            var partsAlertsByAppointment = rawPartsAlertsByAppointment
                .ToDictionary(
                    group => group.Key,
                    group => BuildPartsFollowUpMessage(
                        group.Value
                            .Select(detail => ReplaceFallbackProductLabels(detail, productLabelById))
                            .ToList()));

            var partsFollowUpServicesByAppointment = await BuildPartsFollowUpServicesByAppointmentAsync(
                list,
                rawPartsAlertsByAppointment.Keys,
                packageItemServiceIdsByPackageId);

            return list.Select(a =>
            {
                var requiresDispatchReview = dispatchAlertsByAppointment.TryGetValue(a.Id, out var dispatchReviewNote);
                var requiresPartsFollowUp = partsAlertsByAppointment.TryGetValue(a.Id, out var partsFollowUpNote);
                var assetRows = insuranceAssetLookup.TryGetValue(a.Id, out var loadedAssets)
                    ? loadedAssets
                    : new List<AppointmentInsuranceAsset>();
                var dto = new AppointmentDetailResponseDto
                {
                    Id = a.Id,
                    BranchId = a.BranchId,
                    BranchName = a.Branch?.Name ?? "",
                    CustomerId = a.CustomerId,
                    CustomerName = a.Customer?.FullName ?? "",
                    CustomerPhone = a.Customer?.Phone,
                    VehicleId = a.VehicleId,
                    VehicleLicensePlate = a.Vehicle?.LicensePlate,
                    VehicleModelName = a.Vehicle?.Model?.Brand?.Name != null && a.Vehicle?.Model?.ModelName != null
                        ? $"{a.Vehicle.Model.Brand.Name} {a.Vehicle.Model.ModelName}"
                        : null,
                    GroupSessionId = a.GroupSessionId,
                    GroupSessionCode = a.GroupSession?.GroupCode,
                    GroupSessionStatus = a.GroupSession?.Status,
                    CreatedBy = a.CreatedBy,
                    StartTime = a.StartTime,
                    EndTime = a.EndTime,
                    CreatedAt = a.CreatedAt,
                    RequestedServiceIds = a.RequestedServiceIds,
                    RequestedPackageIds = a.RequestedPackageIds,
                    InsuranceCompanyId = a.InsuranceCompanyId,
                    InsuranceCompanyName = a.InsuranceCompanyId.HasValue &&
                        insuranceCompanyLookup.TryGetValue(a.InsuranceCompanyId.Value, out var insuranceCompanyName)
                        ? insuranceCompanyName
                        : null,
                    InsuranceDeductibleAmount = a.InsuranceDeductibleAmount,
                    InsuranceDeductibleOption = a.InsuranceDeductibleOption,
                    InsuranceClaimMode = a.InsuranceClaimMode,
                    InsuranceQuoteState = a.InsuranceQuoteState,
                    InsuranceCardImageUrls = assetRows
                        .Where(asset => string.Equals(asset.AssetType, "CARD", StringComparison.OrdinalIgnoreCase))
                        .Select(asset => asset.FileUrl)
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .ToList(),
                    InsuranceDamageImageUrls = assetRows
                        .Where(asset => string.Equals(asset.AssetType, "DAMAGE", StringComparison.OrdinalIgnoreCase))
                        .Select(asset => asset.FileUrl)
                        .Where(url => !string.IsNullOrWhiteSpace(url))
                        .ToList(),
                    CustomerNotes = CustomerNoteFormatter.ToDisplayText(a.CustomerNotes),
                    RequiresDispatchReview = requiresDispatchReview,
                    DispatchReviewNote = dispatchReviewNote,
                    RequiresPartsFollowUp = requiresPartsFollowUp,
                    PartsFollowUpNote = partsFollowUpNote,
                    PartsFollowUpServices = partsFollowUpServicesByAppointment.TryGetValue(a.Id, out var partsFollowUpServices)
                        ? partsFollowUpServices
                        : new List<AppointmentPartShortageServiceResponseDto>(),
                    IsReminderSent = a.IsReminderSent,
                    Status = a.Status,
                    CancelReason = a.CancelReason,
                    UpdatedAt = a.UpdatedAt,
                };

                foreach (var sid in ParseServiceIds(a.RequestedServiceIds))
                {
                    dto.ServiceNames.Add(serviceLookup.TryGetValue(sid, out var name) ? name : $"#{sid}");
                }

                foreach (var packageId in ParseServiceIds(a.RequestedPackageIds))
                {
                    var packageName = packageLookup.TryGetValue(packageId, out var name) ? name : $"#{packageId}";
                    dto.PackageNames.Add(packageName);
                    dto.ServiceNames.Add(packageName);
                }

                if (requiresPartsFollowUp &&
                    dto.PartsFollowUpServices.Count == 0 &&
                    rawPartsAlertsByAppointment.TryGetValue(a.Id, out var rawPartsDetails))
                {
                    dto.PartsFollowUpServices = BuildFallbackPartsFollowUpServices(
                        dto.ServiceNames,
                        rawPartsDetails,
                        productLabelById);
                }

                return dto;
            }).ToList();
        }

        private async Task<Dictionary<long, List<AppointmentPartShortageServiceResponseDto>>> BuildPartsFollowUpServicesByAppointmentAsync(
            IReadOnlyCollection<Appointment> appointments,
            IEnumerable<long> appointmentIdsNeedingPartsFollowUp,
            IReadOnlyDictionary<long, List<long>> packageItemServiceIdsByPackageId)
        {
            var targetAppointmentIds = appointmentIdsNeedingPartsFollowUp?.Distinct().ToHashSet() ?? new HashSet<long>();
            if (targetAppointmentIds.Count == 0)
            {
                return new Dictionary<long, List<AppointmentPartShortageServiceResponseDto>>();
            }

            var targetAppointments = appointments
                .Where(appointment => targetAppointmentIds.Contains(appointment.Id) && appointment.VehicleId.HasValue && appointment.VehicleId.Value > 0)
                .ToList();
            if (targetAppointments.Count == 0)
            {
                return new Dictionary<long, List<AppointmentPartShortageServiceResponseDto>>();
            }

            var vehicleIds = targetAppointments
                .Select(appointment => appointment.VehicleId!.Value)
                .Distinct()
                .ToList();

            var vehicleModelByVehicleId = await _context.Vehicles
                .AsNoTracking()
                .Where(vehicle => vehicleIds.Contains(vehicle.Id) && vehicle.IsDeleted != true)
                .ToDictionaryAsync(vehicle => vehicle.Id, vehicle => vehicle.ModelId);

            var serviceIdsByAppointmentId = targetAppointments.ToDictionary(
                appointment => appointment.Id,
                appointment =>
                {
                    var directServiceIds = ParseServiceIds(appointment.RequestedServiceIds);
                    var packageServiceIds = ParseServiceIds(appointment.RequestedPackageIds)
                        .SelectMany(packageId => packageItemServiceIdsByPackageId.TryGetValue(packageId, out var serviceIds)
                            ? serviceIds
                            : Enumerable.Empty<long>());

                    return directServiceIds
                        .Concat(packageServiceIds)
                        .Distinct()
                        .ToList();
                });

            var vehicleModelIds = targetAppointments
                .Select(appointment => appointment.VehicleId.HasValue && vehicleModelByVehicleId.TryGetValue(appointment.VehicleId.Value, out var modelId)
                    ? modelId
                    : 0L)
                .Where(modelId => modelId > 0)
                .Distinct()
                .ToList();
            var serviceIds = serviceIdsByAppointmentId.Values
                .SelectMany(serviceIdsForAppointment => serviceIdsForAppointment)
                .Distinct()
                .ToList();

            if (vehicleModelIds.Count == 0 || serviceIds.Count == 0)
            {
                return new Dictionary<long, List<AppointmentPartShortageServiceResponseDto>>();
            }

            var bomRows = await _context.ServiceBoms
                .AsNoTracking()
                .Where(bom =>
                    vehicleModelIds.Contains(bom.VehicleModelId) &&
                    serviceIds.Contains(bom.ServiceId) &&
                    bom.IsMandatory)
                .Select(bom => new PartBomRow(
                    bom.VehicleModelId,
                    bom.ServiceId,
                    bom.Service.Name,
                    bom.ProductId,
                    bom.Product.Name,
                    bom.Product.Sku,
                    bom.Quantity,
                    bom.Unit))
                .ToListAsync();

            if (bomRows.Count == 0)
            {
                return new Dictionary<long, List<AppointmentPartShortageServiceResponseDto>>();
            }

            var allProductIds = bomRows.Select(row => row.ProductId).Distinct().ToList();
            var branchIds = targetAppointments.Select(appointment => appointment.BranchId).Distinct().ToList();
            var availableByBranchProduct = await _context.Inventories
                .AsNoTracking()
                .Where(inventory =>
                    allProductIds.Contains(inventory.ProductId) &&
                    branchIds.Contains(inventory.Warehouse.BranchId) &&
                    inventory.Warehouse.IsDeleted != true &&
                    inventory.Warehouse.IsActive != false)
                .GroupBy(inventory => new { inventory.Warehouse.BranchId, inventory.ProductId })
                .ToDictionaryAsync(
                    group => (group.Key.BranchId, group.Key.ProductId),
                    group => group.Sum(inventory => inventory.Quantity - (inventory.ReservedQuantity ?? 0m)));

            var bomRowsByVehicleModelAndService = bomRows
                .GroupBy(row => (row.VehicleModelId, row.ServiceId))
                .ToDictionary(group => group.Key, group => group.ToList());

            var result = new Dictionary<long, List<AppointmentPartShortageServiceResponseDto>>();

            foreach (var appointment in targetAppointments)
            {
                if (!appointment.VehicleId.HasValue ||
                    !vehicleModelByVehicleId.TryGetValue(appointment.VehicleId.Value, out var vehicleModelId) ||
                    vehicleModelId <= 0)
                {
                    continue;
                }

                if (!serviceIdsByAppointmentId.TryGetValue(appointment.Id, out var requestedServiceIds) || requestedServiceIds.Count == 0)
                {
                    continue;
                }

                var appointmentBomRows = requestedServiceIds
                    .SelectMany(serviceId => bomRowsByVehicleModelAndService.TryGetValue((vehicleModelId, serviceId), out var rows)
                        ? rows
                        : Enumerable.Empty<PartBomRow>())
                    .ToList();

                if (appointmentBomRows.Count == 0)
                {
                    continue;
                }

                var totalRequiredByProduct = appointmentBomRows
                    .GroupBy(row => row.ProductId)
                    .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));

                var shortageProductIds = totalRequiredByProduct
                    .Where(entry =>
                    {
                        availableByBranchProduct.TryGetValue((appointment.BranchId, entry.Key), out var availableQuantity);
                        return availableQuantity + InventoryQuantityTolerance < entry.Value;
                    })
                    .Select(entry => entry.Key)
                    .ToHashSet();

                if (shortageProductIds.Count == 0)
                {
                    continue;
                }

                var serviceGroups = requestedServiceIds
                    .Select(serviceId =>
                    {
                        var serviceRows = appointmentBomRows
                            .Where(row => row.ServiceId == serviceId && shortageProductIds.Contains(row.ProductId))
                            .ToList();
                        if (serviceRows.Count == 0)
                        {
                            return null;
                        }

                        var firstRow = serviceRows[0];
                        return new AppointmentPartShortageServiceResponseDto
                        {
                            ServiceId = serviceId,
                            ServiceName = string.IsNullOrWhiteSpace(firstRow.ServiceName) ? $"#{serviceId}" : firstRow.ServiceName.Trim(),
                            MissingParts = serviceRows
                                .GroupBy(row => row.ProductId)
                                .Select(group =>
                                {
                                    availableByBranchProduct.TryGetValue((appointment.BranchId, group.Key), out var availableQuantity);
                                    var partRow = group.First();
                                    return new AppointmentPartShortageItemResponseDto
                                    {
                                        ProductId = group.Key,
                                        ProductName = ResolvePartLabel(group.Key, partRow.ProductName, partRow.ProductSku),
                                        RequiredQuantity = group.Sum(row => row.Quantity),
                                        AvailableQuantity = Math.Max(0m, availableQuantity),
                                        Unit = string.IsNullOrWhiteSpace(partRow.Unit) ? null : partRow.Unit.Trim(),
                                    };
                                })
                                .OrderBy(item => item.ProductName)
                                .ToList(),
                        };
                    })
                    .Where(group => group != null)
                    .Cast<AppointmentPartShortageServiceResponseDto>()
                    .ToList();

                if (serviceGroups.Count > 0)
                {
                    result[appointment.Id] = serviceGroups;
                }
            }

            return result;
        }

        private static List<AppointmentPartShortageServiceResponseDto> BuildFallbackPartsFollowUpServices(
            IReadOnlyCollection<string> serviceNames,
            IReadOnlyCollection<string> rawDetails,
            IReadOnlyDictionary<long, string> productLabelById)
        {
            if (serviceNames == null || rawDetails == null)
            {
                return new List<AppointmentPartShortageServiceResponseDto>();
            }

            var normalizedServiceNames = serviceNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct()
                .ToList();

            if (normalizedServiceNames.Count != 1)
            {
                return new List<AppointmentPartShortageServiceResponseDto>();
            }

            var missingParts = ParseFallbackPartShortageItems(rawDetails, productLabelById);
            if (missingParts.Count == 0)
            {
                return new List<AppointmentPartShortageServiceResponseDto>();
            }

            return new List<AppointmentPartShortageServiceResponseDto>
            {
                new()
                {
                    ServiceId = 0,
                    ServiceName = normalizedServiceNames[0],
                    MissingParts = missingParts,
                }
            };
        }

        private static List<AppointmentPartShortageItemResponseDto> ParseFallbackPartShortageItems(
            IReadOnlyCollection<string> rawDetails,
            IReadOnlyDictionary<long, string> productLabelById)
        {
            if (rawDetails == null || rawDetails.Count == 0)
            {
                return new List<AppointmentPartShortageItemResponseDto>();
            }

            var parsedItems = new List<AppointmentPartShortageItemResponseDto>();

            foreach (var detail in rawDetails)
            {
                var normalizedDetail = ReplaceFallbackProductLabels(detail, productLabelById);
                if (string.IsNullOrWhiteSpace(normalizedDetail))
                {
                    continue;
                }

                var segments = normalizedDetail
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var segment in segments)
                {
                    if (string.IsNullOrWhiteSpace(segment))
                    {
                        continue;
                    }

                    var match = PartShortageItemRegex.Match(segment);
                    if (!match.Success)
                    {
                        parsedItems.Add(new AppointmentPartShortageItemResponseDto
                        {
                            ProductId = 0,
                            ProductName = segment.Trim(),
                            RequiredQuantity = 0m,
                            AvailableQuantity = 0m,
                            Unit = null,
                        });
                        continue;
                    }

                    var label = match.Groups["label"].Value.Trim();
                    parsedItems.Add(new AppointmentPartShortageItemResponseDto
                    {
                        ProductId = ExtractFallbackProductId(label),
                        ProductName = label,
                        RequiredQuantity = ParseQuantity(match.Groups["required"].Value),
                        AvailableQuantity = ParseQuantity(match.Groups["available"].Value),
                        Unit = null,
                    });
                }
            }

            return parsedItems
                .Where(item => !string.IsNullOrWhiteSpace(item.ProductName))
                .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var firstItem = group.First();
                    return new AppointmentPartShortageItemResponseDto
                    {
                        ProductId = firstItem.ProductId,
                        ProductName = firstItem.ProductName,
                        RequiredQuantity = group.Sum(item => item.RequiredQuantity),
                        AvailableQuantity = group.Min(item => item.AvailableQuantity),
                        Unit = group.Select(item => item.Unit).FirstOrDefault(unit => !string.IsNullOrWhiteSpace(unit)),
                    };
                })
                .OrderBy(item => item.ProductName)
                .ToList();
        }

        private static string? ExtractTargetBayArea(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            var start = notes.IndexOf(WorkSlotNoteMarkers.TargetBayAreaPrefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += WorkSlotNoteMarkers.TargetBayAreaPrefix.Length;
            var end = notes.IndexOf(']', start);
            if (end <= start)
            {
                return null;
            }

            return notes.Substring(start, end - start).Trim().ToUpperInvariant();
        }

        private static string? ExtractPartsShortageDetails(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return null;
            }

            var start = notes.IndexOf(WorkSlotNoteMarkers.PartsShortageDetailsPrefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += WorkSlotNoteMarkers.PartsShortageDetailsPrefix.Length;
            var end = notes.IndexOf(']', start);
            if (end <= start)
            {
                return null;
            }

            return notes.Substring(start, end - start).Trim();
        }

        private static IEnumerable<long> ExtractFallbackProductIds(IEnumerable<string> details)
        {
            if (details == null)
            {
                yield break;
            }

            foreach (var detail in details)
            {
                if (string.IsNullOrWhiteSpace(detail))
                {
                    continue;
                }

                foreach (Match match in ProductFallbackLabelRegex.Matches(detail))
                {
                    if (long.TryParse(match.Groups["id"].Value, out var productId) && productId > 0)
                    {
                        yield return productId;
                    }
                }
            }
        }

        private static string ReplaceFallbackProductLabels(string detail, IReadOnlyDictionary<long, string> productLabelById)
        {
            if (string.IsNullOrWhiteSpace(detail))
            {
                return string.Empty;
            }

            return ProductFallbackLabelRegex.Replace(detail, match =>
            {
                if (!long.TryParse(match.Groups["id"].Value, out var productId))
                {
                    return match.Value;
                }

                return productLabelById.TryGetValue(productId, out var label) && !string.IsNullOrWhiteSpace(label)
                    ? label
                    : match.Value;
            });
        }

        private static string BuildDispatchAlertMessage(IReadOnlyCollection<string> targetAreas)
        {
            if (targetAreas == null || targetAreas.Count == 0)
            {
                return "Lịch này đang chờ điều phối khoang thủ công. Lễ tân báo quản đốc để gán khoang trước giờ xe vào xưởng.";
            }

            var normalized = targetAreas
                .Where(area => !string.IsNullOrWhiteSpace(area))
                .Select(area => area.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            if (normalized.Count == 0)
            {
                return "Lịch này đang chờ điều phối khoang thủ công. Lễ tân báo quản đốc để gán khoang trước giờ xe vào xưởng.";
            }

            var joinedAreas = string.Join(", ", normalized);
            return $"Lịch này đang chờ điều phối khoang thủ công (mục tiêu: {joinedAreas}). Lễ tân báo quản đốc để gán khoang trước giờ xe vào xưởng.";
        }

        private static string BuildPartsFollowUpMessage(IReadOnlyCollection<string> details)
        {
            const string defaultMessage = "Lịch hẹn đang thiếu phụ tùng bắt buộc. Lễ tân cần gọi khách để xác nhận thời gian xử lý.";
            if (details == null || details.Count == 0)
            {
                return defaultMessage;
            }

            var normalized = details
                .Where(detail => !string.IsNullOrWhiteSpace(detail))
                .Select(detail => detail.Trim())
                .Distinct()
                .ToList();

            if (normalized.Count == 0)
            {
                return defaultMessage;
            }

            return $"Lịch hẹn đang thiếu phụ tùng bắt buộc: {string.Join("; ", normalized)}. Lễ tân cần gọi khách để xác nhận thời gian xử lý.";
        }

        private static string ResolvePartLabel(long productId, string? name, string? sku)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(sku))
            {
                return sku.Trim();
            }

            return $"Sản phẩm #{productId}";
        }

        private static long ExtractFallbackProductId(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return 0;
            }

            var match = ProductFallbackLabelRegex.Match(label);
            return match.Success && long.TryParse(match.Groups["id"].Value, out var productId)
                ? productId
                : 0;
        }

        private static decimal ParseQuantity(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return 0m;
            }

            var trimmed = rawValue.Trim();
            if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.GetCultureInfo("vi-VN"), out var viValue))
            {
                return viValue;
            }

            var normalized = trimmed.Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue)
                ? invariantValue
                : 0m;
        }

        private static IEnumerable<long> ParseServiceIds(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv)) return Enumerable.Empty<long>();
            return csv
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0);
        }
    }
}


