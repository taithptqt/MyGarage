using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Appointments;
using MyGarage.Common;
using MyGarage.Constants;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class AppointmentDetailAssembler : IAppointmentDetailAssembler
    {
        private const decimal InventoryQuantityTolerance = 0.0001m;
        private readonly IAppointmentStore _appointmentStore;

        public AppointmentDetailAssembler(IAppointmentStore appointmentStore)
        {
            _appointmentStore = appointmentStore;
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
                .SelectMany(a => AppointmentDetailParsingHelper.ParseServiceIds(a.RequestedServiceIds))
                .Distinct()
                .ToList();
            var packageIds = list
                .SelectMany(a => AppointmentDetailParsingHelper.ParseServiceIds(a.RequestedPackageIds))
                .Distinct()
                .ToList();

            var serviceLookup = serviceIds.Count == 0
                ? new Dictionary<long, string>()
                : await _appointmentStore.GetStandardServiceNamesByIdsAsync(serviceIds);

            var packageLookup = packageIds.Count == 0
                ? new Dictionary<long, string>()
                : await _appointmentStore.GetPackageNamesByIdsAsync(packageIds);

            var packageItemServiceIdsByPackageId = packageIds.Count == 0
                ? new Dictionary<long, List<long>>()
                : await _appointmentStore.GetPackageItemServiceIdsByPackageIdsAsync(packageIds);

            var insuranceCompanyIds = list
                .Where(a => a.InsuranceCompanyId.HasValue && a.InsuranceCompanyId.Value > 0)
                .Select(a => a.InsuranceCompanyId!.Value)
                .Distinct()
                .ToList();

            var insuranceCompanyLookup = insuranceCompanyIds.Count == 0
                ? new Dictionary<long, string>()
                : await _appointmentStore.GetActiveInsuranceCompanyNamesByIdsAsync(insuranceCompanyIds);

            var insuranceAssets = appointmentIds.Count == 0
                ? new List<AppointmentInsuranceAsset>()
                : await _appointmentStore.GetAppointmentInsuranceAssetsByAppointmentIdsAsync(appointmentIds);

            var insuranceAssetLookup = insuranceAssets
                .GroupBy(asset => asset.AppointmentId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());

            var flaggedSlots = await _appointmentStore.GetFlaggedWorkSlotsByAppointmentIdsAsync(appointmentIds);

            var dispatchAlertsByAppointment = flaggedSlots
                .Where(slot => slot.Notes != null && slot.Notes.Contains(WorkSlotNoteMarkers.FlexPendingBayAssignment))
                .GroupBy(slot => slot.AppointmentId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var targetAreas = group
                            .Select(slot => AppointmentDetailParsingHelper.ExtractTargetBayArea(slot.Notes))
                            .Where(area => !string.IsNullOrWhiteSpace(area))
                            .Select(area => area!)
                            .Distinct()
                            .ToList();

                        return AppointmentDetailParsingHelper.BuildDispatchAlertMessage(targetAreas);
                    });

            var rawPartsAlertsByAppointment = flaggedSlots
                .Where(slot => slot.Notes != null && slot.Notes.Contains(WorkSlotNoteMarkers.PartsShortageAlert))
                .GroupBy(slot => slot.AppointmentId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(slot => AppointmentDetailParsingHelper.ExtractPartsShortageDetails(slot.Notes))
                        .Where(detail => !string.IsNullOrWhiteSpace(detail))
                        .Select(detail => detail!)
                        .Distinct()
                        .ToList());

            var shortageProductIds = rawPartsAlertsByAppointment.Values
                .SelectMany(AppointmentDetailParsingHelper.ExtractFallbackProductIds)
                .Distinct()
                .ToList();

            var productLabelById = shortageProductIds.Count == 0
                ? new Dictionary<long, string>()
                : (await _appointmentStore.GetProductLabelsByIdsAsync(shortageProductIds))
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => AppointmentDetailParsingHelper.ResolvePartLabel(kvp.Value.Id, kvp.Value.Name, kvp.Value.Sku));

            var partsAlertsByAppointment = rawPartsAlertsByAppointment
                .ToDictionary(
                    group => group.Key,
                    group => AppointmentDetailParsingHelper.BuildPartsFollowUpMessage(
                        group.Value
                            .Select(detail => AppointmentDetailParsingHelper.ReplaceFallbackProductLabels(detail, productLabelById))
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

                foreach (var sid in AppointmentDetailParsingHelper.ParseServiceIds(a.RequestedServiceIds))
                {
                    dto.ServiceNames.Add(serviceLookup.TryGetValue(sid, out var name) ? name ?? $"#{sid}" : $"#{sid}");
                }

                foreach (var packageId in AppointmentDetailParsingHelper.ParseServiceIds(a.RequestedPackageIds))
                {
                    var packageName = packageLookup.TryGetValue(packageId, out var name) ? name ?? $"#{packageId}" : $"#{packageId}";
                    dto.PackageNames.Add(packageName);
                    dto.ServiceNames.Add(packageName);
                }

                if (requiresPartsFollowUp &&
                    dto.PartsFollowUpServices.Count == 0 &&
                    rawPartsAlertsByAppointment.TryGetValue(a.Id, out var rawPartsDetails))
                {
                    dto.PartsFollowUpServices = AppointmentDetailParsingHelper.BuildFallbackPartsFollowUpServices(
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

            var vehicleModelByVehicleId = await _appointmentStore.GetActiveVehicleModelIdsByVehicleIdsAsync(vehicleIds);

            var serviceIdsByAppointmentId = targetAppointments.ToDictionary(
                appointment => appointment.Id,
                appointment =>
                {
                    var directServiceIds = AppointmentDetailParsingHelper.ParseServiceIds(appointment.RequestedServiceIds);
                    var packageServiceIds = AppointmentDetailParsingHelper.ParseServiceIds(appointment.RequestedPackageIds)
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

            var bomRows = await _appointmentStore.GetMandatoryServiceBomsAsync(vehicleModelIds, serviceIds);

            if (bomRows.Count == 0)
            {
                return new Dictionary<long, List<AppointmentPartShortageServiceResponseDto>>();
            }

            var allProductIds = bomRows.Select(row => row.ProductId).Distinct().ToList();
            var branchIds = targetAppointments.Select(appointment => appointment.BranchId).Distinct().ToList();
            var availableByBranchProduct = await _appointmentStore.GetAvailableInventoryByBranchAndProductAsync(branchIds, allProductIds);

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
                        : Enumerable.Empty<MandatoryPartBomItem>())
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
                                        ProductName = AppointmentDetailParsingHelper.ResolvePartLabel(group.Key, partRow.ProductName, partRow.ProductSku),
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
    }
}
