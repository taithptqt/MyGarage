using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces
{
    /// <summary>
    /// Evaluates handover readiness and executes final vehicle handover closure for service orders.
    /// </summary>
    public interface IServiceOrderHandoverService
    {
        Task<HandoverReadinessResponseDto?> GetHandoverReadinessAsync(long id);
        Task<bool> ProcessHandoverAsync(long id, HandoverRequestDto request);
    }
}
