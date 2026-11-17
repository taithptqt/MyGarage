using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/retail-returns")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class RetailReturnController : ControllerBase
    {
        private readonly IRetailReturnService _retailReturnService;

        public RetailReturnController(IRetailReturnService retailReturnService)
        {
            _retailReturnService = retailReturnService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<RetailReturnResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (dtos, total) = await _retailReturnService.GetScopedPagedDtosAsync(branchId, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            return Ok(PagedEnvelope<RetailReturnResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var dto = await _retailReturnService.GetScopedDtoByIdAsync(id, branchId, HttpContext.RequestAborted);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RetailReturnResponseDto>> Create([FromBody] RetailReturnCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var actorId = this.GetCurrentUserId();
            if (!actorId.HasValue || actorId.Value <= 0) return Unauthorized();

            var result = await _retailReturnService.CreateScopedPendingAsync(itemDto, actorId.Value, branchId, HttpContext.RequestAborted);
            return result.Status switch
            {
                RetailReturnMutationStatus.NotFound => NotFound(),
                RetailReturnMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ."),
                _ => CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data)
            };
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RetailReturnResponseDto>> Update(long id, [FromBody] RetailReturnUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var result = await _retailReturnService.UpdateScopedPendingAsync(id, itemDto, this.GetCurrentUserId(), branchId, HttpContext.RequestAborted);
            return result.Status switch
            {
                RetailReturnMutationStatus.NotFound => NotFound(),
                RetailReturnMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ."),
                _ => Ok(result.Data)
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var result = await _retailReturnService.DeleteScopedPendingAsync(id, branchId, HttpContext.RequestAborted);
            return result.Status switch
            {
                RetailReturnMutationStatus.NotFound => NotFound(),
                RetailReturnMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ."),
                _ => NoContent()
            };
        }

        [HttpPost("{id:long}/finalizations")]
        public async Task<IActionResult> FinalizeReturn(long id)
        {
            var actorUserId = this.GetCurrentUserId();
            if (!actorUserId.HasValue || actorUserId.Value <= 0) return Unauthorized();
            if (!this.TryResolveBranchScope(null, out var scopedBranch, out var denied)) return denied!;
            var result = await _retailReturnService.FinalizeReturnAsync(id, actorUserId.Value, scopedBranch, HttpContext.RequestAborted);
            return result.Outcome switch
            {
                RetailReturnFinalizeStatus.NotFound => this.NotFoundProblem(result.Message),
                RetailReturnFinalizeStatus.BadRequest => this.BadRequestProblem(result.Message),
                RetailReturnFinalizeStatus.AlreadyCompleted => Ok(new { message = result.Message, retailReturnId = result.RetailReturnId }),
                _ => Ok(new { retailReturnId = result.RetailReturnId, status = result.Status, totalRefundAmount = result.TotalRefundAmount, message = result.Message })
            };
        }
    }
}
