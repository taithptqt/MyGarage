using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Extensions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/v1/invoices")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoiceController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<InvoiceResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (dtos, total) = await _invoiceService.GetScopedPageDtosAsync(branchId, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            var envelope = PagedEnvelope<InvoiceResponseDto>.Create(dtos, total, normalizedPage, normalizedSize);
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var dto = await _invoiceService.GetScopedDtoByIdAsync(id, branchId, HttpContext.RequestAborted);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceResponseDto>> Create([FromBody] InvoiceCreateDto itemDto)
        {
            var actorUserId = this.GetCurrentUserId();
            if (actorUserId == null)
            {
                return Unauthorized();
            }

            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied)) return (ActionResult)denied!;

            var result = await _invoiceService.CreateOrUpdateForContextAsync(
                itemDto,
                actorUserId.Value,
                scopedBranchId,
                this.ToUserActorContext(),
                HttpContext.RequestAborted);

            return result.Status switch
            {
                InvoiceMutationStatus.NotFound => NotFound(),
                InvoiceMutationStatus.Forbidden => Forbid(),
                InvoiceMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ."),
                InvoiceMutationStatus.Created => CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data),
                _ => Ok(result.Data)
            };
        }

        [HttpPost("completed-payment-backfills")]
        public async Task<ActionResult<InvoiceBackfillResponseDto>> BackfillCompletedPayments()
        {
            if (!this.IsAdminStaff()) return Forbid();
            var result = await _invoiceService.BackfillCompletedPaymentsAsync();
            return Ok(result);
        }

        [HttpPut("{id}")]
        public Task<ActionResult<InvoiceResponseDto>> Update(long id, [FromBody] InvoiceUpdateDto itemDto)
        {
            return Task.FromResult<ActionResult<InvoiceResponseDto>>(
                this.ProblemMessage(405, "Hóa đơn là chứng từ bất biến và không được phép chỉnh sửa trực tiếp."));
        }

        [HttpDelete("{id}")]
        public Task<IActionResult> Delete(long id)
        {
            return Task.FromResult<IActionResult>(
                this.ProblemMessage(405, "Hóa đơn là chứng từ bất biến và không được phép xóa trực tiếp."));
        }
    }
}
