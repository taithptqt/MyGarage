using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.Constants;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class ServiceOrderWorkshopBillingService : IServiceOrderWorkshopBillingService
    {
        private const string WorkshopDetailItemType = "LABOR";
        private const string WorkshopDetailDescription = "Dịch vụ khách yêu cầu trước";
        private const string WorkshopDetailUnit = "Lần";
        private const string WorkshopDetailStatus = nameof(ServiceOrderDetailStatusCode.APPROVED);
        private const int MaxServiceOrderInsuranceCardImageCount = 2;
        private const int MaxServiceOrderInsuranceDamageImageCount = 6;

        private readonly IServiceOrderIntakeContextStore _intakeContextStore;
        private readonly IServiceOrderBillingQueryStore _queryStore;
        private readonly IServiceOrderBillingCommandStore _commandStore;
        private readonly IServiceOrderTransactionCommandStore _transactionStore;
        private readonly IServiceOrderIntakeService _intakeService;
        private readonly ISystemClock _clock;

        public ServiceOrderWorkshopBillingService(
            IServiceOrderIntakeContextStore intakeContextStore,
            IServiceOrderBillingQueryStore queryStore,
            IServiceOrderBillingCommandStore commandStore,
            IServiceOrderTransactionCommandStore transactionStore,
            IServiceOrderIntakeService intakeService,
            ISystemClock clock)
        {
            _intakeContextStore = intakeContextStore;
            _queryStore = queryStore;
            _commandStore = commandStore;
            _transactionStore = transactionStore;
            _intakeService = intakeService;
            _clock = clock;
        }

        public async Task<WorkshopServiceSelection> BuildSelectionAsync(
            IEnumerable<long>? serviceIds,
            IEnumerable<long>? packageIds)
        {
            var directServiceIds = ServiceOrderValueHelper.NormalizePositiveLongList(serviceIds);
            var requestedPackageIds = ServiceOrderValueHelper.NormalizePositiveLongList(packageIds);
            if (directServiceIds.Count == 0 && requestedPackageIds.Count == 0)
            {
                return WorkshopServiceSelection.Empty;
            }

            var packages = requestedPackageIds.Count == 0
                ? new List<Package>()
                : await _intakeContextStore.GetActivePackagesWithItemsAsync(requestedPackageIds);

            if (packages.Count != requestedPackageIds.Count)
            {
                throw new InvalidOperationException("Có gói dịch vụ đã chọn không còn hợp lệ hoặc đã bị xóa.");
            }

            var packageServiceIds = packages
                .SelectMany(package => package.PackageItems ?? new List<PackageItem>())
                .Where(item =>
                    item.StandardServiceId > 0 &&
                    item.StandardService != null &&
                    item.StandardService.IsDeleted != true &&
                    item.StandardService.IsActive != false)
                .Select(item => item.StandardServiceId);

            var effectiveServiceIds = directServiceIds
                .Concat(packageServiceIds)
                .Distinct()
                .ToList();

            var serviceMap = effectiveServiceIds.Count == 0
                ? new Dictionary<long, StandardService>()
                : await _intakeContextStore.GetActiveStandardServicesMapAsync(effectiveServiceIds);

            if (serviceMap.Count != effectiveServiceIds.Count)
            {
                throw new InvalidOperationException("Có dịch vụ trong gói hoặc dịch vụ đã chọn không còn hợp lệ hoặc đã bị xóa.");
            }

            return new WorkshopServiceSelection(
                directServiceIds,
                requestedPackageIds,
                effectiveServiceIds,
                serviceMap,
                packages);
        }

        public async Task EnsureSelectionCompatibleWithVehicleAsync(
            WorkshopServiceSelection selection,
            long vehicleId)
        {
            if (selection == null || vehicleId <= 0)
            {
                return;
            }

            var vehicleModelProjection = await _intakeContextStore.GetVehicleEngineFuelInfoAsync(vehicleId);

            if (vehicleModelProjection == null || vehicleModelProjection.ModelId <= 0)
            {
                return;
            }

            if (EngineTypeCompatibilityRules.ParseVehicleEngineFamilies(vehicleModelProjection.FuelType).Count == 0)
            {
                return;
            }

            var incompatibleServices = (selection.ServiceMap?.Values ?? new Dictionary<long, StandardService>().Values)
                .Where(service => !EngineTypeCompatibilityRules.IsServiceCompatibleWithVehicleFuel(service.EngineType, vehicleModelProjection.FuelType))
                .Select(service => service.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (incompatibleServices.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Dịch vụ không phù hợp với loại động cơ của dòng xe đã chọn: {string.Join(", ", incompatibleServices)}.");
        }

        public async Task AddInitialServiceDetailsAsync(
            ServiceOrder serviceOrder,
            WorkshopServiceSelection selection,
            long? actorStaffId,
            long? requestedInsuranceCompanyId)
        {
            if (serviceOrder == null || serviceOrder.Id <= 0)
            {
                return;
            }

            var now = VietnamTime.Now;
            var insuranceCompanyId = await ResolveOrderInsuranceCompanyIdAsync(serviceOrder, requestedInsuranceCompanyId);
            var currentOrderCategory = ServiceOrderValueHelper.NormalizeToken(serviceOrder.OrderCategory);
            var hasInsuranceSignalService = selection.EffectiveServiceIds.Any(serviceId =>
                selection.ServiceMap.TryGetValue(serviceId, out var service) &&
                InsuranceBillingRules.IsInsuranceSignalService(service));
            var insuranceOrderContext = insuranceCompanyId.HasValue &&
                (string.Equals(currentOrderCategory, InsuranceBillingRules.InsuranceRepairCategoryCode, StringComparison.OrdinalIgnoreCase) ||
                 hasInsuranceSignalService ||
                 selection.HasInsuranceSignalPackage);

            if (insuranceOrderContext || selection.HasInsuranceSignalPackage)
            {
                serviceOrder.OrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;
            }

            serviceOrder.RequestedPackageIds = selection.RequestedPackageIds;

            var details = new List<ServiceOrderDetail>(selection.EffectiveServiceIds.Count);
            foreach (var serviceId in selection.EffectiveServiceIds)
            {
                if (!selection.ServiceMap.TryGetValue(serviceId, out var service))
                {
                    continue;
                }

                var detail = new ServiceOrderDetail
                {
                    ServiceOrderId = serviceOrder.Id,
                    StandardServiceId = service.Id,
                    CreatedAt = now,
                    CreatedBy = actorStaffId,
                    UpdatedAt = now,
                    UpdatedBy = actorStaffId
                };
                ApplyWorkshopServiceDetailValues(
                    detail,
                    service,
                    actorStaffId,
                    now,
                    insuranceCompanyId,
                    insuranceOrderContext);
                details.Add(detail);
            }

            if (details.Count > 0)
            {
                _commandStore.AddServiceOrderDetails(details);
            }

            var subTotal = details.Sum(detail => detail.SubTotal);
            var taxAmount = details.Sum(detail => detail.TaxAmount);
            serviceOrder.SubTotalAmount = subTotal;
            serviceOrder.TaxAmount = taxAmount;
            serviceOrder.FinalAmount = subTotal + taxAmount - ServiceOrderValueHelper.ResolveServiceOrderTotalDiscountAmount(serviceOrder);
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = actorStaffId;

            await _transactionStore.SaveChangesInTransactionAsync();
        }

        public async Task SyncWorkshopServiceDetailsAsync(
            ServiceOrder order,
            WorkshopServiceSelection selection,
            long? actorStaffId,
            long? insuranceCompanyId,
            bool insuranceOrderContext)
        {
            var now = order.UpdatedAt ?? VietnamTime.Now;
            var selectedServiceIds = selection.EffectiveServiceIds;
            var serviceMap = selection.ServiceMap;
            var editableDetails = await _queryStore.GetEditableServiceDetailsAsync(order.Id);

            var editableByServiceId = editableDetails
                .Where(d => d.StandardServiceId.HasValue && d.StandardServiceId.Value > 0)
                .GroupBy(d => d.StandardServiceId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());

            var keptServiceIds = new HashSet<long>();
            foreach (var detail in editableDetails.Where(d => !d.StandardServiceId.HasValue || d.StandardServiceId.Value <= 0))
            {
                _commandStore.RemoveServiceOrderDetail(detail);
            }

            foreach (var pair in editableByServiceId)
            {
                var serviceId = pair.Key;
                var rows = pair.Value;

                if (!selectedServiceIds.Contains(serviceId))
                {
                    foreach (var row in rows)
                    {
                        _commandStore.RemoveServiceOrderDetail(row);
                    }
                    continue;
                }

                var keep = rows[0];
                ApplyWorkshopServiceDetailValues(
                    keep,
                    serviceMap[serviceId],
                    actorStaffId,
                    now,
                    insuranceCompanyId,
                    insuranceOrderContext);
                if (rows.Count > 1)
                {
                    foreach (var duplicate in rows.Skip(1))
                    {
                        _commandStore.RemoveServiceOrderDetail(duplicate);
                    }
                }
                keptServiceIds.Add(serviceId);
            }

            foreach (var serviceId in selectedServiceIds.Where(serviceId => !keptServiceIds.Contains(serviceId)))
            {
                var service = serviceMap[serviceId];
                var detail = new ServiceOrderDetail
                {
                    ServiceOrderId = order.Id,
                    StandardServiceId = service.Id,
                    CreatedAt = now,
                    CreatedBy = actorStaffId,
                    UpdatedAt = now,
                    UpdatedBy = actorStaffId
                };
                ApplyWorkshopServiceDetailValues(
                    detail,
                    service,
                    actorStaffId,
                    now,
                    insuranceCompanyId,
                    insuranceOrderContext);
                _commandStore.AddServiceOrderDetail(detail);
            }

            var subTotal = selectedServiceIds.Sum(serviceId => serviceMap[serviceId].BasePrice);
            var taxAmount = selectedServiceIds.Sum(serviceId =>
                CalculateTaxAmount(serviceMap[serviceId].BasePrice, serviceMap[serviceId].TaxRate ?? 0m));
            order.SubTotalAmount = subTotal;
            order.TaxAmount = taxAmount;
            order.FinalAmount = subTotal + taxAmount - ServiceOrderValueHelper.ResolveServiceOrderTotalDiscountAmount(order);
        }

        public async Task<long?> ResolveOrderInsuranceCompanyIdAsync(
            ServiceOrder order,
            long? requestedInsuranceCompanyId)
        {
            if (requestedInsuranceCompanyId.HasValue && requestedInsuranceCompanyId.Value > 0)
            {
                await _intakeService.EnsureInsuranceCompanyExistsAsync(requestedInsuranceCompanyId.Value);
                return requestedInsuranceCompanyId.Value;
            }

            if (!order.AppointmentId.HasValue || order.AppointmentId.Value <= 0)
            {
                return null;
            }

            var appointmentInsuranceCompanyId = await _intakeContextStore.GetAppointmentInsuranceCompanyIdAsync(order.AppointmentId.Value);

            if (appointmentInsuranceCompanyId.HasValue && appointmentInsuranceCompanyId.Value > 0)
            {
                await _intakeService.EnsureInsuranceCompanyExistsAsync(appointmentInsuranceCompanyId.Value);
                return appointmentInsuranceCompanyId.Value;
            }

            return null;
        }

        public async Task PersistInsuranceAssetsAsync(
            long serviceOrderId,
            IEnumerable<string>? insuranceCardImageUrls,
            IEnumerable<string>? insuranceDamageImageUrls)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var cardUrls = SanitizeServiceOrderInsuranceAssetUrls(
                insuranceCardImageUrls,
                MaxServiceOrderInsuranceCardImageCount);
            var damageUrls = SanitizeServiceOrderInsuranceAssetUrls(
                insuranceDamageImageUrls,
                MaxServiceOrderInsuranceDamageImageCount);

            if (cardUrls.Count == 0 && damageUrls.Count == 0)
            {
                return;
            }

            var rows = new List<ServiceOrderInsuranceAsset>(cardUrls.Count + damageUrls.Count);
            rows.AddRange(cardUrls.Select((url, index) => new ServiceOrderInsuranceAsset
            {
                ServiceOrderId = serviceOrderId,
                AssetType = AppointmentInsuranceAssetTypes.Card,
                FileUrl = url,
                SortOrder = index
            }));
            rows.AddRange(damageUrls.Select((url, index) => new ServiceOrderInsuranceAsset
            {
                ServiceOrderId = serviceOrderId,
                AssetType = AppointmentInsuranceAssetTypes.Damage,
                FileUrl = url,
                SortOrder = index
            }));

            _commandStore.AddInsuranceAssets(rows);
            await _transactionStore.SaveChangesInTransactionAsync();
            await MarkServiceOrderInsuranceUploadsAttachedAsync(serviceOrderId, cardUrls.Concat(damageUrls));
        }

        public async Task ReplaceInsuranceAssetsAsync(
            long serviceOrderId,
            IEnumerable<string>? insuranceCardImageUrls,
            IEnumerable<string>? insuranceDamageImageUrls)
        {
            if (serviceOrderId <= 0)
            {
                return;
            }

            var existingRows = await _intakeContextStore.GetInsuranceAssetsByOrderIdAsync(serviceOrderId);
            if (existingRows.Count > 0)
            {
                _commandStore.RemoveInsuranceAssets(existingRows);
                await _transactionStore.SaveChangesInTransactionAsync();
            }

            await PersistInsuranceAssetsAsync(
                serviceOrderId,
                insuranceCardImageUrls,
                insuranceDamageImageUrls);
        }

        private static void ApplyWorkshopServiceDetailValues(
            ServiceOrderDetail detail,
            StandardService service,
            long? actorStaffId,
            DateTime nowUtc,
            long? insuranceCompanyId,
            bool insuranceOrderContext)
        {
            var taxRate = service.TaxRate ?? 0m;
            var subTotal = service.BasePrice;
            var taxAmount = CalculateTaxAmount(subTotal, taxRate);
            var finalAmount = subTotal + taxAmount;

            detail.ItemType = WorkshopDetailItemType;
            detail.ItemName = service.Name;
            detail.Description = WorkshopDetailDescription;
            detail.Quantity = 1m;
            detail.Unit = WorkshopDetailUnit;
            detail.UnitPriceAtTime = service.BasePrice;
            detail.SubTotal = subTotal;
            detail.TaxRate = taxRate;
            detail.TaxAmount = taxAmount;
            detail.FinalAmount = finalAmount;
            ApplyInsuranceBilling(detail, service, insuranceCompanyId, insuranceOrderContext);
            detail.Status = WorkshopDetailStatus;
            detail.StandardServiceId = service.Id;
            detail.ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
            detail.IsSupplementary = false;
            detail.SupplementaryReasonCode = null;
            detail.SourceQuotationId = null;
            detail.SupplementaryRequestedByStaffId = null;
            detail.SupplementaryRequestedAt = null;
            detail.CustomerApprovedAt = null;
            detail.UpdatedAt = nowUtc;
            detail.UpdatedBy = actorStaffId;
        }

        private static void ApplyInsuranceBilling(
            ServiceOrderDetail detail,
            StandardService service,
            long? insuranceCompanyId,
            bool insuranceOrderContext)
        {
            if (InsuranceBillingRules.ShouldBillInsurance(service, insuranceOrderContext) && insuranceCompanyId.HasValue)
            {
                detail.PayerType = InsuranceBillingRules.PayerTypeInsurance;
                detail.InsuranceCompanyId = insuranceCompanyId.Value;
                detail.InsuranceRequestedAmount = detail.FinalAmount;
                detail.InsuranceApprovedAmount = 0m;
                detail.CustomerCoPayAmount = 0m;
                detail.GarageAbsorbedAmount = 0m;
                detail.InsuranceStatus = nameof(ServiceOrderDetailInsuranceStatusCode.PENDING_SUBMISSION);
                return;
            }

            detail.PayerType = InsuranceBillingRules.PayerTypeCustomer;
            detail.InsuranceCompanyId = null;
            detail.InsuranceRequestedAmount = 0m;
            detail.InsuranceApprovedAmount = 0m;
            detail.CustomerCoPayAmount = detail.FinalAmount;
            detail.GarageAbsorbedAmount = 0m;
            detail.InsuranceStatus = nameof(ServiceOrderDetailInsuranceStatusCode.NOT_APPLICABLE);
        }

        private async Task MarkServiceOrderInsuranceUploadsAttachedAsync(
            long serviceOrderId,
            IEnumerable<string> fileUrls)
        {
            var normalizedUrls = fileUrls?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (serviceOrderId <= 0 || normalizedUrls.Count == 0)
            {
                return;
            }

            var stagedUploads = await _intakeContextStore.GetStagedInsuranceUploadsAsync(
                normalizedUrls,
                ServiceOrderInsuranceUploadStatuses.Staged);

            if (stagedUploads.Count == 0)
            {
                return;
            }

            foreach (var upload in stagedUploads)
            {
                upload.Status = ServiceOrderInsuranceUploadStatuses.Attached;
                upload.ServiceOrderId = serviceOrderId;
                upload.UpdatedAt = _clock.UtcNow;
            }

            await _transactionStore.SaveChangesInTransactionAsync();
        }

        private static List<string> SanitizeServiceOrderInsuranceAssetUrls(
            IEnumerable<string>? urls,
            int maxCount)
        {
            var results = new List<string>();
            if (urls == null)
            {
                return results;
            }

            foreach (var rawUrl in urls)
            {
                if (results.Count >= maxCount)
                {
                    break;
                }

                var url = (rawUrl ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (url.Length > 500)
                {
                    throw new InvalidOperationException("Đường dẫn ảnh bảo hiểm không hợp lệ.");
                }

                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl) ||
                    (parsedUrl.Scheme != Uri.UriSchemeHttp && parsedUrl.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException("Đường dẫn ảnh bảo hiểm không hợp lệ.");
                }

                if (!results.Contains(url, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(url);
                }
            }

            return results;
        }

        private static decimal CalculateTaxAmount(decimal amount, decimal taxRate)
        {
            return Math.Round(amount * taxRate / 100m, 2, MidpointRounding.AwayFromZero);
        }
    }
}
