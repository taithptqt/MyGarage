using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Application.Payroll;

public interface IPayrollRunsApplicationService
{
    Task<PayrollRunEndpointActionResult<PagedEnvelope<PayrollRunEndpointPayloadDto>>> GetAllAsync(
        long? scopedBranchId,
        int? page = null,
        int? pageSize = null,
        CancellationToken ct = default);

    Task<PayrollRunEndpointActionResult<PayrollRunPreviewEndpointDto>> PreviewAsync(
        long targetBranchId,
        string? period = null,
        CancellationToken ct = default);

    Task<PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>> CreateAsync(
        long targetBranchId,
        long? currentUserId,
        PayrollRunCreateEndpointDto? payload,
        CancellationToken ct = default);

    Task<PayrollRunEndpointActionResult<PayrollRunEndpointPayloadDto>> ApproveAsync(
        long id,
        long? scopedBranchId,
        long? actorId,
        CancellationToken ct = default);
}
