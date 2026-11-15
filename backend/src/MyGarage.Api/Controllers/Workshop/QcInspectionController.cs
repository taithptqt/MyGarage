using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [Route("api/v1/qc-inspections")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class QcInspectionController : ControllerBase
    {
        private const string QcResultPass = "PASS";
        private const string QcResultFail = "FAIL";
        private const string QcResultNa = "N/A";

        private readonly IQcInspectionService _qcInspectionService;
        private readonly IQcInspectionDataService _dataService;

        public QcInspectionController(IQcInspectionService qcInspectionService, IQcInspectionDataService dataService)
        {
            _qcInspectionService = qcInspectionService;
            _dataService = dataService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<QcInspectionResponseDto>>> GetAll(
            [FromQuery] long? serviceOrderId = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                var (paged, total) = await _dataService.GetPagedByServiceOrderAsync(
                    serviceOrderId.Value,
                    normalizedPage,
                    normalizedSize,
                    HttpContext.RequestAborted);
                var dtos = _qcInspectionService.MapToDtos(paged);
                return Ok(PagedEnvelope<QcInspectionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
            }
            else
            {
                var (paged, total) = await _qcInspectionService.GetPageByIdAsync(normalizedPage, normalizedSize);
                var dtos = _qcInspectionService.MapToDtos(paged);
                return Ok(PagedEnvelope<QcInspectionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
            }
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
            var inspectorId = itemDto.QcStaffId > 0
                ? itemDto.QcStaffId
                : this.GetCurrentUserId() ?? 0;

            if (inspectorId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(itemDto.ServiceOrderId, inspectorId, HttpContext.RequestAborted))
            {
                return this.BadRequestProblem("Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties).");
            }

            var responseDto = await _qcInspectionService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
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
            request ??= new QcInspectionItemsUpsertRequestDto();
            var serviceOrderId = await _dataService.GetServiceOrderIdAsync(id, HttpContext.RequestAborted);
            if (!serviceOrderId.HasValue)
            {
                return this.NotFoundProblem($"QC inspection #{id} not found.");
            }

            var currentUserId = this.GetCurrentUserId() ?? 0;
            if (currentUserId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(serviceOrderId.Value, currentUserId, HttpContext.RequestAborted))
            {
                return this.BadRequestProblem("Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties).");
            }

            var incomingItems = request.Items ?? [];
            if (!request.OverwriteExisting && incomingItems.Count == 0)
            {
                return this.BadRequestProblem("items is required when overwriteExisting=false.");
            }

            var validatedItems = new List<(QcInspectionChecklistItemUpsertDto Input, string ItemName, string? NormalizedResult)>(incomingItems.Count);
            foreach (var dto in incomingItems)
            {
                var itemName = (dto.ItemName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(itemName))
                {
                    return this.BadRequestProblem("Each checklist item requires itemName.");
                }

                var normalizedResult = NormalizeQcResult(dto.Result);
                if (dto.Result is not null && normalizedResult is null)
                {
                    return this.BadRequestProblem("result must be PASS, FAIL, N/A or empty.");
                }

                validatedItems.Add((dto, itemName, normalizedResult));
            }

            var summary = await _dataService.UpsertChecklistItemsAsync(
                id,
                request.OverwriteExisting,
                validatedItems,
                HttpContext.RequestAborted);
            if (summary is null)
            {
                return this.NotFoundProblem($"QC inspection #{id} not found.");
            }

            return Ok(summary);
        }

        [HttpPost("{id}/passes")]
        public async Task<IActionResult> Pass(long id, [FromBody] QcPassRequestDto request)
        {
            var serviceOrderId = await _dataService.GetServiceOrderIdAsync(id, HttpContext.RequestAborted);
            if (!serviceOrderId.HasValue) return NotFound();

            var inspectorId = request.InspectorId > 0 ? request.InspectorId : this.GetCurrentUserId() ?? 0;
            if (inspectorId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(serviceOrderId.Value, inspectorId, HttpContext.RequestAborted))
            {
                return this.BadRequestProblem("Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties).");
            }

            var result = await _qcInspectionService.PassAsync(id, request);
            if (!result) return NotFound();
            return Ok(new { message = "QC Pass recorded and Service Order marked as READY." });
        }

        [HttpPost("{id}/failures")]
        public async Task<IActionResult> Fail(long id, [FromBody] QcFailRequestDto request)
        {
            var serviceOrderId = await _dataService.GetServiceOrderIdAsync(id, HttpContext.RequestAborted);
            if (!serviceOrderId.HasValue) return NotFound();

            var inspectorId = request.InspectorId > 0 ? request.InspectorId : this.GetCurrentUserId() ?? 0;
            if (inspectorId > 0 && await _dataService.IsTechnicianWhoWorkedOnOrderAsync(serviceOrderId.Value, inspectorId, HttpContext.RequestAborted))
            {
                return this.BadRequestProblem("Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties).");
            }

            var result = await _qcInspectionService.FailAsync(id, request);
            if (!result) return NotFound();
            return Ok(new { message = "QC Fail recorded and Service Order reverted to REPAIRING." });
        }

        private static string? NormalizeQcResult(string? result)
        {
            if (string.IsNullOrWhiteSpace(result))
            {
                return null;
            }

            var normalized = result.Trim().ToUpperInvariant();
            return normalized switch
            {
                "PASS" => QcResultPass,
                "FAIL" => QcResultFail,
                "NA" => QcResultNa,
                "N/A" => QcResultNa,
                _ => null
            };
        }
    }
}






