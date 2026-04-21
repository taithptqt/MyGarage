using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Common;
using MyGarage.Enums;
using MyGarage.Models;

namespace MyGarage.Services
{
    public partial class AppointmentService
    {
        private async Task EnsureServiceOrderExistsForArrivedAppointmentAsync(
            Appointment appointment,
            DateTime arriveTime,
            long? actorStaffId)
        {
            if (!IsArrivalStatusForServiceOrder(appointment.Status))
            {
                return;
            }

            if (appointment.Id <= 0)
            {
                return;
            }

            var existingOrder = await _context.ServiceOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(order => order.AppointmentId == appointment.Id);
            if (existingOrder != null)
            {
                await SyncWorkSlotsAfterArrivalAsync(appointment, existingOrder.Id, arriveTime);
                return;
            }

            if (appointment.CustomerId <= 0)
            {
                throw new InvalidOperationException("Cannot create service order: invalid appointment customer.");
            }

            var vehicleId = appointment.VehicleId.GetValueOrDefault();
            if (vehicleId <= 0)
            {
                throw new InvalidOperationException("Cannot create service order: appointment does not have a vehicle.");
            }

            if (appointment.BranchId <= 0)
            {
                throw new InvalidOperationException("Cannot create service order: invalid branch.");
            }

            var estimatedHandover = appointment.EndTime < arriveTime
                ? arriveTime.AddMinutes(ExecutionBufferMinutes)
                : appointment.EndTime.AddMinutes(ExecutionBufferMinutes);

            var now = _clock.LocalNow;
            var serviceOrder = new ServiceOrder
            {
                CustomerId = appointment.CustomerId,
                VehicleId = vehicleId,
                AppointmentId = appointment.Id,
                GroupSessionId = appointment.GroupSessionId,
                OriginBranchId = appointment.BranchId,
                ExecuteBranchId = appointment.BranchId,
                ServiceAdvisorId = actorStaffId,
                ReceptionistId = actorStaffId,
                CustomerNotes = CustomerNoteFormatter.ToDisplayText(appointment.CustomerNotes),
                RequestedPackageIds = appointment.RequestedPackageIds,
                InsuranceDeductibleAmount = appointment.InsuranceDeductibleAmount,
                InsuranceDeductibleOption = appointment.InsuranceDeductibleOption,
                InsuranceClaimMode = appointment.InsuranceClaimMode,
                InsuranceQuoteState = appointment.InsuranceQuoteState,
                CheckInTime = arriveTime,
                EstimatedHandover = estimatedHandover,
                PromisedHandoverTime = estimatedHandover,
                Status = nameof(ServiceOrderLifecycleStatus.NEW),
                CreatedBy = actorStaffId,
                UpdatedBy = actorStaffId,
                CreatedAt = now,
                UpdatedAt = now
            };

            var createdOrder = await _serviceOrderService.AddAsync(serviceOrder);
            await SyncAppointmentServicesToServiceOrderDetailsAsync(
                createdOrder,
                appointment,
                appointment.RequestedServiceIds,
                appointment.RequestedPackageIds,
                actorStaffId,
                now);
            await CopyAppointmentInsuranceAssetsToServiceOrderAsync(appointment.Id, createdOrder.Id);
            await SyncWorkSlotsAfterArrivalAsync(appointment, createdOrder.Id, arriveTime);
        }

        private static bool IsArrivalStatusForServiceOrder(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return status.Equals(nameof(AppointmentStatus.ARRIVED), StringComparison.OrdinalIgnoreCase)
                || status.Equals(nameof(AppointmentStatus.ARRIVED_LATE), StringComparison.OrdinalIgnoreCase);
        }

        private async Task SyncAppointmentServicesToServiceOrderDetailsAsync(
            ServiceOrder serviceOrder,
            Appointment appointment,
            string? requestedServiceIdsRaw,
            string? requestedPackageIdsRaw,
            long? actorStaffId,
            DateTime now)
        {
            if (serviceOrder.Id <= 0)
            {
                return;
            }

            var requestedServiceIds = ParseServiceIds(requestedServiceIdsRaw)
                .Concat(await ResolvePackageItemServiceIdsAsync(requestedPackageIdsRaw))
                .Distinct()
                .ToList();
            var requestedPackages = await LoadRequestedPackagesAsync(requestedPackageIdsRaw);
            var insuranceCompanyId = appointment.InsuranceCompanyId.HasValue && appointment.InsuranceCompanyId.Value > 0
                ? appointment.InsuranceCompanyId
                : null;
            var packageInsuranceContext = insuranceCompanyId.HasValue &&
                requestedPackages.Any(InsuranceBillingRules.IsInsuranceSignalPackage);

            if (requestedServiceIds.Count == 0)
            {
                if (packageInsuranceContext)
                {
                    serviceOrder.OrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;
                }

                serviceOrder.SubTotalAmount = 0m;
                serviceOrder.TaxAmount = 0m;
                serviceOrder.FinalAmount = 0m;
                serviceOrder.UpdatedAt = now;
                serviceOrder.UpdatedBy = actorStaffId;
                await SaveChangesInTransactionAsync();
                return;
            }

            var serviceMap = await _context.StandardServices
                .AsNoTracking()
                .Where(s =>
                    requestedServiceIds.Contains(s.Id) &&
                    s.IsDeleted != true &&
                    s.IsActive != false)
                .ToDictionaryAsync(s => s.Id);

            var selectedServices = requestedServiceIds
                .Where(id => serviceMap.ContainsKey(id))
                .Select(id => serviceMap[id])
                .ToList();

            if (selectedServices.Count == 0)
            {
                if (packageInsuranceContext)
                {
                    serviceOrder.OrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;
                }

                serviceOrder.SubTotalAmount = 0m;
                serviceOrder.TaxAmount = 0m;
                serviceOrder.FinalAmount = 0m;
                serviceOrder.UpdatedAt = now;
                serviceOrder.UpdatedBy = actorStaffId;
                await SaveChangesInTransactionAsync();
                return;
            }

            var insuranceOrderContext = insuranceCompanyId.HasValue &&
                (selectedServices.Any(InsuranceBillingRules.IsInsuranceSignalService) || packageInsuranceContext);

            if (insuranceOrderContext)
            {
                serviceOrder.OrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;
            }

            var details = new List<ServiceOrderDetail>(selectedServices.Count);
            foreach (var service in selectedServices)
            {
                var taxRate = service.TaxRate ?? 0m;
                var subTotal = service.BasePrice;
                var taxAmount = CalculateTaxAmount(subTotal, taxRate);
                details.Add(new ServiceOrderDetail
                {
                    ServiceOrderId = serviceOrder.Id,
                    StandardServiceId = service.Id,
                    ItemType = "LABOR",
                    ItemName = service.Name,
                    Description = "Dịch vụ khách chọn khi đặt lịch",
                    Quantity = 1m,
                    Unit = "Lần",
                    UnitPriceAtTime = subTotal,
                    SubTotal = subTotal,
                    TaxRate = taxRate,
                    TaxAmount = taxAmount,
                    FinalAmount = subTotal + taxAmount,
                    PayerType = InsuranceBillingRules.PayerTypeCustomer,
                    Status = nameof(ServiceOrderDetailStatusCode.APPROVED),
                    ServiceSource = nameof(ServiceOrderDetailServiceSourceCode.INITIAL),
                    IsSupplementary = false,
                    SupplementaryReasonCode = null,
                    SourceQuotationId = null,
                    SupplementaryRequestedByStaffId = null,
                    SupplementaryRequestedAt = null,
                    CustomerApprovedAt = null,
                    CreatedAt = now,
                    CreatedBy = actorStaffId,
                    UpdatedAt = now,
                    UpdatedBy = actorStaffId
                });

                ApplyInsuranceBilling(detail: details[^1], service, insuranceCompanyId, insuranceOrderContext);
            }

            _context.ServiceOrderDetails.AddRange(details);

            var totalSubAmount = details.Sum(d => d.SubTotal);
            var totalTaxAmount = details.Sum(d => d.TaxAmount);
            serviceOrder.SubTotalAmount = totalSubAmount;
            serviceOrder.TaxAmount = totalTaxAmount;
            serviceOrder.FinalAmount = totalSubAmount + totalTaxAmount - ResolveServiceOrderTotalDiscountAmount(serviceOrder);
            serviceOrder.UpdatedAt = now;
            serviceOrder.UpdatedBy = actorStaffId;
            await SaveChangesInTransactionAsync();
        }

        private static decimal ResolveServiceOrderTotalDiscountAmount(ServiceOrder serviceOrder)
        {
            if (serviceOrder == null)
            {
                return 0m;
            }

            return (serviceOrder.ServiceOrderDiscounts ?? new List<ServiceOrderDiscount>())
                .Where(x => x != null)
                .Sum(x => x.AppliedAmount > 0m ? x.AppliedAmount : Math.Max(0m, x.DiscountValue));
        }

        private static decimal CalculateTaxAmount(decimal amount, decimal taxRate)
        {
            return Math.Round(amount * taxRate / 100m, 2, MidpointRounding.AwayFromZero);
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
    }
}
