using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IApprovalRequestService : IGenericService<ApprovalRequest>
    {
        Task<ApprovalRequestResponseDto?> GetDtoByIdAsync(long id);
        Task<ApprovalRequestResponseDto> CreateDtoAsync(ApprovalRequestCreateDto itemDto);
        Task<ApprovalRequestResponseDto?> UpdateDtoAsync(long id, ApprovalRequestUpdateDto itemDto);
    }
}
