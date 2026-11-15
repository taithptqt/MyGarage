using MyGarage.Models;
using MyGarage.DTOs.Requests;
using System.Threading.Tasks;

namespace MyGarage.Interfaces
{
    public interface IMaterialRequestService : IGenericService<MaterialRequest>
    {
        Task<MaterialRequest?> CreateFromQuotationAsync(long quotationId, long actorId);
        Task<MaterialRequest> IssuePartsAsync(MaterialRequestIssuanceDto request);
        Task<MaterialRequest> RejectRequestAsync(long requestId, long actorStaffId, string? reason);
    }
}
