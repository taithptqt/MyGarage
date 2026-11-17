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
    [Route("api/v1/qc-inspections")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class QcInspectionController : ControllerBase
    {
        private readonly IQcInspectionService _qcInspectionService;

        public QcInspectionController(IQcInspectionService qcInspectionService)
        {
            _qcInspectionService = qcInspectionService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<QcInspectionResponseDto>>> GetAll(
            [FromQuery] long? serviceOrderId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (dtos, total) = await _qcInspectionService.GetPagedDtosAsync(
                serviceOrderId,
                normalizedPage,
                normalizedSize,
                HttpContext.RequestAborted);
            return Ok(PagedEnvelope<QcInspectionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<QcInspectionResponseDto>> GetById(long id)
        {
            var dto = await _qcInspectionService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<QcInspectionResponseDto>> Create([FromBody] QcInspectionCreateDto itemDto)
        {
            var result = await _qcInspectionService.CreateWithSeparationOfDutiesAsync(
                itemDto,
                this.GetCurrentUserId(),
                HttpContext.RequestAborted);
            if (result.Status == QcMutationStatus.BadRequest)
            {
                return this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ.");
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<QcInspectionResponseDto>> Update(long id, [FromBody] QcInspectionUpdateDto itemDto)
        {
            var responseDto = await _qcInspectionService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _qcInspectionService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpPost("{id:long}/items")]
        public async Task<IActionResult> UpsertChecklistItems(long id, [FromBody] QcInspectionItemsUpsertRequestDto? request)
        {
            var result = await _qcInspectionService.UpsertChecklistItemsAsync(
                id,
                request,
                this.GetCurrentUserId(),
                HttpContext.RequestAborted);

            return result.Status switch
            {
                QcMutationStatus.NotFound => this.NotFoundProblem(result.ErrorMessage ?? $"QC inspection #{id} not found."),
                QcMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ."),
                _ => Ok(result.Data)
            };
        }

        [HttpPost("{id}/passes")]
        public async Task<IActionResult> Pass(long id, [FromBody] QcPassRequestDto request)
        {
            var result = await _qcInspectionService.PassAsync(
                id,
                request,
                this.GetCurrentUserId(),
                HttpContext.RequestAborted);

            return result.Status switch
            {
                QcMutationStatus.NotFound => NotFound(),
                QcMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ."),
                _ => Ok(new { message = "QC Pass recorded and Service Order marked as READY." })
            };
        }

        [HttpPost("{id}/failures")]
        public async Task<IActionResult> Fail(long id, [FromBody] QcFailRequestDto request)
        {
            var result = await _qcInspectionService.FailAsync(
                id,
                request,
                this.GetCurrentUserId(),
                HttpContext.RequestAborted);

            return result.Status switch
            {
                QcMutationStatus.NotFound => NotFound(),
                QcMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Yêu cầu không hợp lệ."),
                _ => Ok(new { message = "QC Fail recorded and Service Order reverted to REPAIRING." })
            };
        }
    }
}

