using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/invoice-details")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class InvoiceDetailController : ControllerBase
    {
        private readonly IInvoiceDetailService _invoiceDetailService;

        public InvoiceDetailController(IInvoiceDetailService invoiceDetailService)
        {
            _invoiceDetailService = invoiceDetailService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvoiceDetailResponseDto>>> GetAll()
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var dtos = await _invoiceDetailService.GetScopedDtosAsync(branchId, HttpContext.RequestAborted);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceDetailResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var dto = await _invoiceDetailService.GetScopedDtoByIdAsync(id, branchId, HttpContext.RequestAborted);
            if (dto == null)
            {
                return NotFound();
            }

            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceDetailResponseDto>> Create([FromBody] InvoiceDetailCreateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var result = await _invoiceDetailService.CreateScopedAsync(branchId, itemDto, HttpContext.RequestAborted);
            switch (result.Status)
            {
                case InvoiceDetailCommandStatus.NotFound:
                    return NotFound();
                case InvoiceDetailCommandStatus.BadRequest:
                    return this.BadRequestProblem(result.ErrorMessage ?? "Invalid request.");
                default:
                    return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data!);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceDetailResponseDto>> Update(long id, [FromBody] InvoiceDetailUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var result = await _invoiceDetailService.UpdateScopedAsync(id, branchId, itemDto, HttpContext.RequestAborted);
            switch (result.Status)
            {
                case InvoiceDetailCommandStatus.NotFound:
                    return NotFound();
                case InvoiceDetailCommandStatus.BadRequest:
                    return this.BadRequestProblem(result.ErrorMessage ?? "Invalid request.");
                default:
                    return Ok(result.Data!);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied))
            {
                return denied!;
            }

            var result = await _invoiceDetailService.DeleteScopedAsync(id, branchId, HttpContext.RequestAborted);
            return result.Status switch
            {
                InvoiceDetailCommandStatus.NotFound => NotFound(),
                InvoiceDetailCommandStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Invalid request."),
                _ => NoContent()
            };
        }
    }
}
