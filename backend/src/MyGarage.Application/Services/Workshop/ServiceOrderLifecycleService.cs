using System;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    /// <summary>
    /// Coordinates service order editability gates, handover operations, closure side-effects, and lifecycle notifications.
    /// </summary>
    public sealed class ServiceOrderLifecycleService : IServiceOrderLifecycleService
    {
        private readonly IServiceOrderEditCapabilityService _editCapabilityService;
        private readonly IServiceOrderClosureService _closureService;
        private readonly IServiceOrderHandoverService _handoverService;
        private readonly IServiceOrderCustomerLifecycleNotifier _customerLifecycleNotifier;

        public ServiceOrderLifecycleService(
            IServiceOrderEditCapabilityService editCapabilityService,
            IServiceOrderClosureService closureService,
            IServiceOrderHandoverService handoverService,
            IServiceOrderCustomerLifecycleNotifier customerLifecycleNotifier)
        {
            _editCapabilityService = editCapabilityService ?? throw new ArgumentNullException(nameof(editCapabilityService));
            _closureService = closureService ?? throw new ArgumentNullException(nameof(closureService));
            _handoverService = handoverService ?? throw new ArgumentNullException(nameof(handoverService));
            _customerLifecycleNotifier = customerLifecycleNotifier ?? throw new ArgumentNullException(nameof(customerLifecycleNotifier));
        }

        public Task<ServiceOrderEditCapabilityResponseDto?> GetEditCapabilityAsync(long id)
            => _editCapabilityService.GetEditCapabilityAsync(id);

        public Task EnsureWorkshopEditableAsync(long id)
            => _editCapabilityService.EnsureWorkshopEditableAsync(id);

        public Task EnsureQuoteAndPartsEditableAsync(long id)
            => _editCapabilityService.EnsureQuoteAndPartsEditableAsync(id);

        public Task EnsureAssignmentEditableAsync(long id)
            => _editCapabilityService.EnsureAssignmentEditableAsync(id);

        public Task EnsurePartsEditableAsync(long id)
            => _editCapabilityService.EnsurePartsEditableAsync(id);

        public Task<HandoverReadinessResponseDto?> GetHandoverReadinessAsync(long id)
            => _handoverService.GetHandoverReadinessAsync(id);

        public Task<bool> ProcessHandoverAsync(long id, HandoverRequestDto request)
            => _handoverService.ProcessHandoverAsync(id, request);

        public Task HandleOrderClosureAsync(ServiceOrder order, string? previousStatus, long? actorUserId)
            => _closureService.HandleOrderClosureAsync(order, previousStatus, actorUserId);

        public void EnqueueAfterStatusChange(ServiceOrder order, string previousStatus)
            => _customerLifecycleNotifier.EnqueueAfterStatusChange(order, previousStatus);
    }
}
