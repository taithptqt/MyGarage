using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IServiceOrderService : IGenericService<ServiceOrder>
    {
        Task<ServiceOrderBatchCreateResult> AddWorkshopBatchAsync(long? actorUserId, ServiceOrderBatchCreateRequestDto dto);
        Task<ServiceOrder?> UpdateWorkshopAsync(long id, WorkshopServiceOrderEditRequestDto dto, long? actorUserId = null);
        Task<ServiceOrderEditCapabilityResponseDto?> GetEditCapabilityAsync(long id);
        Task EnsureWorkshopEditableAsync(long id);
        Task EnsureQuoteAndPartsEditableAsync(long id);
        Task EnsureAssignmentEditableAsync(long id);
        Task EnsurePartsEditableAsync(long id);
    }
}
