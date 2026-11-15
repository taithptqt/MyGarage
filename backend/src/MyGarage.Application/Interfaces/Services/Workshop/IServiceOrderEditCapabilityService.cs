using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    /// <summary>
    /// Evaluates edit locks, active override requests, and section-level editability for service orders.
    /// </summary>
    public interface IServiceOrderEditCapabilityService
    {
        Task<ServiceOrderEditCapabilityResponseDto?> GetEditCapabilityAsync(long id);
        Task EnsureWorkshopEditableAsync(long id);
        Task EnsureQuoteAndPartsEditableAsync(long id);
        Task EnsureAssignmentEditableAsync(long id);
        Task EnsurePartsEditableAsync(long id);
    }
}
