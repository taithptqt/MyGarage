using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IVoucherApprovalRequestService : IGenericService<VoucherApprovalRequest>
    {
        Task<IEnumerable<VoucherApprovalRequest>> GetAllAsync(string? status = null);
        Task<VoucherApprovalRequest?> GetDetailedByIdAsync(long id);
        Task<VoucherApprovalRequest?> ApproveAsync(long id, VoucherApprovalDecisionDto decision);
        Task<VoucherApprovalRequest?> RejectAsync(long id, VoucherApprovalDecisionDto decision);
        List<VoucherApprovalRequestResponseDto> MapToDtos(IEnumerable<VoucherApprovalRequest> items);
        Task<VoucherApprovalRequestResponseDto?> GetDetailedDtoByIdAsync(long id);
        Task<VoucherApprovalRequestResponseDto> CreateDtoAsync(VoucherApprovalRequestCreateDto dto);
        Task<VoucherApprovalRequestResponseDto?> ApproveDtoAsync(long id, VoucherApprovalDecisionDto decision);
        Task<VoucherApprovalRequestResponseDto?> RejectDtoAsync(long id, VoucherApprovalDecisionDto decision);
    }
}

