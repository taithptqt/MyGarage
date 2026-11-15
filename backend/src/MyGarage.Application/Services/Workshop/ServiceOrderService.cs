using System;
using System.Globalization;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Workshop;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public sealed class ServiceOrderService : GenericService<ServiceOrder>, IServiceOrderService
    {
        private readonly IServiceOrderCreationCommandStore _commandStore;
        private readonly IServiceOrderStatusQueryStore _queryStore;
        private readonly IServiceOrderIntakeService _intakeService;
        private readonly IServiceOrderLifecycleService _lifecycleService;
        private readonly IServiceOrderWorkshopWorkflowService _workshopWorkflowService;

        public ServiceOrderService(
            IServiceOrderRepository repository,
            IServiceOrderCreationCommandStore commandStore,
            IServiceOrderStatusQueryStore queryStore,
            IServiceOrderIntakeService intakeService,
            IServiceOrderLifecycleService lifecycleService,
            IServiceOrderWorkshopWorkflowService workshopWorkflowService) : base(repository)
        {
            _commandStore = commandStore ?? throw new ArgumentNullException(nameof(commandStore));
            _queryStore = queryStore ?? throw new ArgumentNullException(nameof(queryStore));
            _intakeService = intakeService ?? throw new ArgumentNullException(nameof(intakeService));
            _lifecycleService = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
            _workshopWorkflowService = workshopWorkflowService ?? throw new ArgumentNullException(nameof(workshopWorkflowService));
        }

        internal ServiceOrderService(
            IServiceOrderRepository repository,
            IServiceOrderCommandStore commandStore,
            IServiceOrderQueryStore queryStore,
            IGroupSessionService groupSessionService,
            IServiceOrderCustomerLifecycleNotifier customerLifecycleNotifier,
            IServiceOrderEditCapabilityService editCapabilityService,
            IServiceOrderIntakeService intakeService,
            IServiceOrderWorkshopBillingService workshopBillingService,
            IServiceOrderClosureService closureService,
            IServiceOrderHandoverService handoverService)
            : this(
                repository,
                commandStore,
                queryStore,
                intakeService,
                CreateLifecycleService(
                    editCapabilityService,
                    closureService,
                    handoverService,
                    customerLifecycleNotifier),
                new ServiceOrderWorkshopWorkflowService(
                    commandStore,
                    queryStore,
                    groupSessionService,
                    intakeService,
                    workshopBillingService,
                    CreateLifecycleService(
                        editCapabilityService,
                        closureService,
                        handoverService,
                        customerLifecycleNotifier)))
        {
        }

        public override async Task<ServiceOrder> AddAsync(ServiceOrder entity)
        {
            ArgumentNullException.ThrowIfNull(entity);
            var checkInTime = entity.CheckInTime ?? VietnamTime.Now;
            var executeBranchId = entity.ExecuteBranchId > 0 ? entity.ExecuteBranchId : entity.OriginBranchId;
            await _intakeService.EnsureBranchOpenForCheckInAsync(executeBranchId, checkInTime);

            return await _commandStore.CreateWithUniqueCodeAsync(
                entity,
                GenerateServiceOrderCode,
                async order =>
                {
                    var createdOrder = await _repository.AddAsync(order);
                    await _intakeService.EnsureCheckInRequestExistsAsync(createdOrder.Id);
                });
        }

        public override async Task<ServiceOrder?> UpdateAsync(object id, ServiceOrder entity)
        {
            if (!TryParsePositiveServiceOrderId(id, out var serviceOrderId))
                throw new InvalidOperationException("Mã lệnh sửa chữa không hợp lệ.");

            ArgumentNullException.ThrowIfNull(entity);

            var checkInTime = entity.CheckInTime ?? VietnamTime.Now;
            var executeBranchId = entity.ExecuteBranchId > 0 ? entity.ExecuteBranchId : entity.OriginBranchId;
            await _intakeService.EnsureBranchOpenForCheckInAsync(executeBranchId, checkInTime);
            await EnsureWorkshopEditableAsync(serviceOrderId);

            var previousLifecycleStatus = await _queryStore.GetServiceOrderStatusAsync(serviceOrderId) ?? string.Empty;
            var updated = await base.UpdateAsync(id, entity);
            if (updated != null)
                await _lifecycleService.HandleOrderClosureAsync(updated, previousLifecycleStatus, entity.UpdatedBy);

            return updated;
        }

        public override async Task<bool> DeleteAsync(object id)
        {
            if (!TryParsePositiveServiceOrderId(id, out var serviceOrderId))
                throw new InvalidOperationException("Mã lệnh sửa chữa không hợp lệ.");

            await EnsureWorkshopEditableAsync(serviceOrderId);
            return await base.DeleteAsync(id);
        }

        public Task<ServiceOrderBatchCreateResult> AddWorkshopBatchAsync(long? actorUserId, ServiceOrderBatchCreateRequestDto dto)
            => _workshopWorkflowService.AddWorkshopBatchAsync(actorUserId, dto, AddAsync);

        public Task<ServiceOrder?> UpdateWorkshopAsync(long id, WorkshopServiceOrderEditRequestDto dto, long? actorUserId = null)
            => _workshopWorkflowService.UpdateWorkshopAsync(id, dto, actorUserId);

        public Task<ServiceOrderEditCapabilityResponseDto?> GetEditCapabilityAsync(long id) => _lifecycleService.GetEditCapabilityAsync(id);
        public Task EnsureWorkshopEditableAsync(long id) => _lifecycleService.EnsureWorkshopEditableAsync(id);
        public Task EnsureQuoteAndPartsEditableAsync(long id) => _lifecycleService.EnsureQuoteAndPartsEditableAsync(id);
        public Task EnsureAssignmentEditableAsync(long id) => _lifecycleService.EnsureAssignmentEditableAsync(id);
        public Task EnsurePartsEditableAsync(long id) => _lifecycleService.EnsurePartsEditableAsync(id);
        public Task<HandoverReadinessResponseDto?> GetHandoverReadinessAsync(long id) => _lifecycleService.GetHandoverReadinessAsync(id);
        public Task<bool> ProcessHandoverAsync(long id, HandoverRequestDto request) => _lifecycleService.ProcessHandoverAsync(id, request);

        internal static string? NormalizeText(string? value) => ServiceOrderValueHelper.NormalizeText(value);
        internal static string ResolveOrderStatus(string? status) => ServiceOrderValueHelper.ResolveOrderStatus(status);
        internal static string? NormalizeToken(string? value) => ServiceOrderValueHelper.NormalizeToken(value);
        internal static void ApplyInsuranceSnapshot(ServiceOrder order, bool insuranceEnabled, long? insuranceCompanyId, decimal? deductibleAmount, string? deductibleOption, string? claimMode, string? quoteState)
            => ServiceOrderValueHelper.ApplyInsuranceSnapshot(order, insuranceEnabled, insuranceCompanyId, deductibleAmount, deductibleOption, claimMode, quoteState);

        private static ServiceOrderLifecycleService CreateLifecycleService(
            IServiceOrderEditCapabilityService editCapabilityService,
            IServiceOrderClosureService closureService,
            IServiceOrderHandoverService handoverService,
            IServiceOrderCustomerLifecycleNotifier customerLifecycleNotifier)
            => new(editCapabilityService, closureService, handoverService, customerLifecycleNotifier);

        private static bool TryParsePositiveServiceOrderId(object id, out long serviceOrderId)
        {
            serviceOrderId = id switch
            {
                long l => l,
                int i => i,
                not null when long.TryParse(Convert.ToString(id, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => 0
            };
            return serviceOrderId > 0;
        }

        private static string GenerateServiceOrderCode()
            => $"SO-{VietnamTime.Now.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }
}
