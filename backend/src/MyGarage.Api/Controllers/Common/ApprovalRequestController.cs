using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Application.Approvals;
using MyGarage.Application.Common.Pagination;

namespace MyGarage.Controllers
{
    [Route("api/v1/approval-requests")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ApprovalRequestController : ControllerBase
    {
        private readonly IApprovalRequestService _approvalRequestService;
        private readonly ApprovalListQuery _list;

        public ApprovalRequestController(IApprovalRequestService approvalRequestService, ApprovalListQuery list)
        {
            _approvalRequestService = approvalRequestService;
            _list = list;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<ApprovalListItem>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            CancellationToken cancellationToken = default)
        {
            var (current, size) = PaginationExtensions.NormalizePagination(page, pageSize);
            var result = await _list.ExecuteAsync(current, size, cancellationToken);
            return Ok(PagedEnvelope<ApprovalListItem>.Create(result.Items, result.Total, current, size));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApprovalRequestResponseDto>> GetById(long id)
        {
            var dto = await _approvalRequestService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ApprovalRequestResponseDto>> Create([FromBody] ApprovalRequestCreateDto itemDto)
        {
            var responseDto = await _approvalRequestService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApprovalRequestResponseDto>> Update(long id, [FromBody] ApprovalRequestUpdateDto itemDto)
        {
            var responseDto = await _approvalRequestService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _approvalRequestService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






