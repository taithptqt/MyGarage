using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    /// <summary>
    /// Orchestrates workshop batch creation and workshop editing workflows for service orders.
    /// </summary>
    public sealed class ServiceOrderWorkshopWorkflowService : IServiceOrderWorkshopWorkflowService
    {
        private static readonly HashSet<string> AllowedWorkshopOrderCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "QUICK_SERVICE",
            "GENERAL_REPAIR",
            "MAINTENANCE",
            "INSURANCE_REPAIR",
            "REWORK",
            "WARRANTLY",
            "WARRANTY",
        };

        private readonly IServiceOrderTransactionCommandStore _commandStore;
        private readonly IServiceOrderWorkflowQueryStore _queryStore;
        private readonly IGroupSessionService _groupSessionService;
        private readonly IServiceOrderIntakeService _intakeService;
        private readonly IServiceOrderWorkshopBillingService _workshopBillingService;
        private readonly IServiceOrderLifecycleService _lifecycleService;

        public ServiceOrderWorkshopWorkflowService(
            IServiceOrderTransactionCommandStore commandStore,
            IServiceOrderWorkflowQueryStore queryStore,
            IGroupSessionService groupSessionService,
            IServiceOrderIntakeService intakeService,
            IServiceOrderWorkshopBillingService workshopBillingService,
            IServiceOrderLifecycleService lifecycleService)
        {
            _commandStore = commandStore ?? throw new ArgumentNullException(nameof(commandStore));
            _queryStore = queryStore ?? throw new ArgumentNullException(nameof(queryStore));
            _groupSessionService = groupSessionService ?? throw new ArgumentNullException(nameof(groupSessionService));
            _intakeService = intakeService ?? throw new ArgumentNullException(nameof(intakeService));
            _workshopBillingService = workshopBillingService ?? throw new ArgumentNullException(nameof(workshopBillingService));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
        }

        public async Task<ServiceOrderBatchCreateResult> AddWorkshopBatchAsync(
            long? actorUserId,
            ServiceOrderBatchCreateRequestDto dto,
            Func<ServiceOrder, Task<ServiceOrder>> createOrderAsync)
        {
            ArgumentNullException.ThrowIfNull(createOrderAsync);

            if (dto == null)
                throw new InvalidOperationException("Dữ liệu tạo lệnh sửa chữa không hợp lệ.");

            var items = dto.Items?.Where(item => item != null).ToList() ?? new List<WorkshopServiceOrderCreateDto>();
            if (items.Count == 0)
                throw new InvalidOperationException("Danh sách lệnh sửa chữa không được để trống.");

            return await _commandStore.ExecuteInTransactionAsync<ServiceOrderBatchCreateResult>(async () =>
            {
                var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
                long? groupSessionId = null;

                if (dto.CreateGroupSession)
                {
                    var customerId = dto.CustomerId.GetValueOrDefault();
                    var branchId = dto.BranchId.GetValueOrDefault();
                    if (customerId <= 0 || branchId <= 0)
                        throw new InvalidOperationException("CustomerId và BranchId phải lớn hơn 0 khi tạo nhóm lệnh.");

                    await _intakeService.EnsureCustomerExistsAsync(customerId);
                    var group = await _groupSessionService.CreateAsync(
                        customerId,
                        branchId,
                        ServiceOrderValueHelper.NormalizeText(dto.CustomerNote),
                        actorStaffId);
                    groupSessionId = group.Id;
                }

                var createdOrders = new List<ServiceOrder>(items.Count);
                foreach (var item in items)
                {
                    var selection = await _workshopBillingService.BuildSelectionAsync(item.ServiceIds, item.PackageIds);
                    var prepared = await _intakeService.PrepareWorkshopOrderEntityAsync(item, actorStaffId, groupSessionId);
                    await _workshopBillingService.EnsureSelectionCompatibleWithVehicleAsync(selection, prepared.VehicleId);
                    prepared.RequestedPackageIds = selection.RequestedPackageIds;
                    var createdOrder = await createOrderAsync(prepared);
                    await _workshopBillingService.AddInitialServiceDetailsAsync(createdOrder, selection, actorStaffId, item.InsuranceCompanyId);
                    await _workshopBillingService.PersistInsuranceAssetsAsync(createdOrder.Id, item.InsuranceCardImageUrls, item.InsuranceDamageImageUrls);
                    createdOrders.Add(createdOrder);
                }

                return new ServiceOrderBatchCreateResult { GroupSessionId = groupSessionId, Orders = createdOrders };
            });
        }

        public async Task<ServiceOrder?> UpdateWorkshopAsync(
            long id,
            WorkshopServiceOrderEditRequestDto dto,
            long? actorUserId = null)
        {
            if (id <= 0) throw new InvalidOperationException("Mã lệnh sửa chữa không hợp lệ.");
            if (dto == null) throw new InvalidOperationException("Dữ liệu cập nhật lệnh sửa chữa không hợp lệ.");

            var order = await _queryStore.GetServiceOrderWithDiscountsAsync(id);
            if (order == null) return null;

            await _lifecycleService.EnsureWorkshopEditableAsync(id);

            var selection = await _workshopBillingService.BuildSelectionAsync(dto.ServiceIds, dto.PackageIds);
            if (!selection.HasSelection) throw new InvalidOperationException("Vui lòng chọn ít nhất một dịch vụ.");
            await _workshopBillingService.EnsureSelectionCompatibleWithVehicleAsync(selection, order.VehicleId);

            var normalizedOrderCategory = ServiceOrderValueHelper.NormalizeToken(dto.OrderCategory);
            if (!string.IsNullOrWhiteSpace(normalizedOrderCategory) && !AllowedWorkshopOrderCategories.Contains(normalizedOrderCategory))
                throw new InvalidOperationException($"Phân loại lệnh sửa chữa không hợp lệ: {dto.OrderCategory}.");

            var actorStaffId = await _groupSessionService.ResolveStaffActorIdAsync(actorUserId);
            var insuranceCompanyId = await _workshopBillingService.ResolveOrderInsuranceCompanyIdAsync(order, dto.InsuranceCompanyId);
            var currentOrderCategory = !string.IsNullOrWhiteSpace(normalizedOrderCategory)
                ? normalizedOrderCategory
                : ServiceOrderValueHelper.NormalizeToken(order.OrderCategory);
            var hasInsuranceSignalService = selection.EffectiveServiceIds.Any(
                serviceId => InsuranceBillingRules.IsInsuranceSignalService(selection.ServiceMap[serviceId]));
            var insuranceOrderContext = insuranceCompanyId.HasValue &&
                (string.Equals(currentOrderCategory, InsuranceBillingRules.InsuranceRepairCategoryCode, StringComparison.OrdinalIgnoreCase) ||
                 hasInsuranceSignalService || selection.HasInsuranceSignalPackage);

            return await _commandStore.ExecuteInTransactionAsync(async () =>
            {
                var checkInTime = dto.CheckInTime ?? order.CheckInTime ?? VietnamTime.Now;
                var estimatedHandover = dto.EstimatedHandover ?? order.EstimatedHandover;
                var promisedHandover = dto.PromisedHandoverTime ?? order.PromisedHandoverTime;
                await _intakeService.EnsureBranchOpenForCheckInAsync(order.ExecuteBranchId, checkInTime);

                if (estimatedHandover.HasValue && estimatedHandover.Value < checkInTime)
                    throw new InvalidOperationException("Thời gian dự kiến hoàn thành phải lớn hơn hoặc bằng thời điểm xe vào xưởng.");
                if (promisedHandover.HasValue && promisedHandover.Value < checkInTime)
                    throw new InvalidOperationException("Thời gian cam kết trả xe phải lớn hơn hoặc bằng thời điểm xe vào xưởng.");

                if (!string.IsNullOrWhiteSpace(normalizedOrderCategory)) order.OrderCategory = normalizedOrderCategory;
                else if (insuranceOrderContext || selection.HasInsuranceSignalPackage) order.OrderCategory = InsuranceBillingRules.InsuranceRepairCategoryCode;

                order.ServiceAdvisorId = ResolveServiceAdvisorForWorkshopEdit(dto.ServiceAdvisorId, order.ServiceAdvisorId, actorStaffId);
                order.OldPartsReturnOption = ServiceOrderValueHelper.NormalizeText(dto.OldPartsReturnOption) ?? order.OldPartsReturnOption;
                order.RequiredDepositAmount = Math.Max(dto.RequiredDepositAmount ?? 0m, 0m);
                order.CheckInTime = checkInTime;
                order.EstimatedHandover = estimatedHandover;
                order.PromisedHandoverTime = promisedHandover;
                var previousLifecycleStatus = order.Status;
                order.Status = ServiceOrderValueHelper.ResolveOrderStatus(dto.Status ?? order.Status);
                order.RequestedPackageIds = selection.RequestedPackageIds;
                ServiceOrderValueHelper.ApplyInsuranceSnapshot(
                    order,
                    insuranceOrderContext,
                    insuranceCompanyId,
                    dto.InsuranceDeductibleAmount,
                    dto.InsuranceDeductibleOption,
                    dto.InsuranceClaimMode,
                    dto.InsuranceQuoteState);

                order.UpdatedAt = VietnamTime.Now;
                order.UpdatedBy = actorStaffId;

                await _workshopBillingService.SyncWorkshopServiceDetailsAsync(order, selection, actorStaffId, insuranceCompanyId, insuranceOrderContext);
                _lifecycleService.EnqueueAfterStatusChange(order, previousLifecycleStatus);
                await _commandStore.SaveChangesInTransactionAsync();
                await _lifecycleService.HandleOrderClosureAsync(order, previousLifecycleStatus, actorStaffId);
                await _workshopBillingService.ReplaceInsuranceAssetsAsync(order.Id, dto.InsuranceCardImageUrls, dto.InsuranceDamageImageUrls);
                return order;
            });
        }

        private static long? ResolveServiceAdvisorForWorkshopEdit(long? requestedId, long? currentId, long? actorStaffId)
            => requestedId is > 0 ? requestedId.Value : currentId is > 0 ? currentId.Value : actorStaffId is > 0 ? actorStaffId.Value : null;
    }
}
