using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    /// <summary>
    /// Coordinates service order editability gates, handover operations, closure side-effects, and lifecycle notifications.
    /// </summary>
    public interface IServiceOrderLifecycleService
    {
        Task<ServiceOrderEditCapabilityResponseDto?> GetEditCapabilityAsync(long id);
        Task EnsureWorkshopEditableAsync(long id);
        Task EnsureQuoteAndPartsEditableAsync(long id);
        Task EnsureAssignmentEditableAsync(long id);
        Task EnsurePartsEditableAsync(long id);
        Task<HandoverReadinessResponseDto?> GetHandoverReadinessAsync(long id);
        Task<bool> ProcessHandoverAsync(long id, HandoverRequestDto request);
        Task HandleOrderClosureAsync(ServiceOrder order, string? previousStatus, long? actorUserId);
        void EnqueueAfterStatusChange(ServiceOrder order, string previousStatus);
    }
}
