using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/qc-inspections")]
    [Route("api/v1/qc-inspections")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class QcInspectionController : ControllerBase
    {
        private const string QcResultPass = "PASS";
        private const string QcResultFail = "FAIL";
        private const string QcResultNa = "N/A";
        private const string QcStatusPass = nameof(Enums.QcInspectionStatusCode.PASS);
        private const string QcStatusFail = nameof(Enums.QcInspectionStatusCode.FAIL);
        private const string ServiceOrderQcPending = nameof(Enums.ServiceOrderQcStatusCode.PENDING);
        private const string ServiceOrderQcPass = nameof(Enums.ServiceOrderQcStatusCode.PASS);
        private const string ServiceOrderQcFail = nameof(Enums.ServiceOrderQcStatusCode.FAIL);

        private readonly IQcInspectionService _qcInspectionService;
        private readonly IMapper _mapper;
        private readonly IGarageContext _db;

        public QcInspectionController(IQcInspectionService qcInspectionService, IMapper mapper, IGarageContext db)
        {
            _qcInspectionService = qcInspectionService;
            _mapper = mapper;
            _db = db;
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
                var query = _db.QcInspections.AsNoTracking()
                    .Where(x => x.ServiceOrderId == serviceOrderId.Value)
                    .OrderByDescending(x => x.Id);
                var total = await query.CountAsync();
                var offset = ((long)normalizedPage - 1) * normalizedSize;
                var paged = offset >= total
                    ? new List<QcInspection>()
                    : await query.Skip((int)offset).Take(normalizedSize).ToListAsync();
                var dtos = _mapper.Map<List<QcInspectionResponseDto>>(paged);
                return Ok(PagedEnvelope<QcInspectionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
            }
            else
            {
                var (paged, total) = await _qcInspectionService.GetPageByIdAsync(normalizedPage, normalizedSize);
                var dtos = _mapper.Map<List<QcInspectionResponseDto>>(paged);
                return Ok(PagedEnvelope<QcInspectionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<QcInspectionResponseDto>> GetById(long id)
        {
            var item = await _qcInspectionService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<QcInspectionResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<QcInspectionResponseDto>> Create([FromBody] QcInspectionCreateDto itemDto)
        {
            var inspectorId = itemDto.QcStaffId > 0
                ? itemDto.QcStaffId
                : this.GetCurrentUserId() ?? 0;

            if (inspectorId > 0 && await IsTechnicianWhoWorkedOnOrderAsync(itemDto.ServiceOrderId, inspectorId))
            {
                return BadRequest(new { message = "Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties)." });
            }

            var entity = _mapper.Map<QcInspection>(itemDto);
            var createdItem = await _qcInspectionService.AddAsync(entity);
            var responseDto = _mapper.Map<QcInspectionResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<QcInspectionResponseDto>> Update(long id, [FromBody] QcInspectionUpdateDto itemDto)
        {
            var existingItem = await _qcInspectionService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _qcInspectionService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<QcInspectionResponseDto>(updatedItem);
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
            var inspection = await _db.QcInspections.FirstOrDefaultAsync(x => x.Id == id);
            if (inspection is null)
            {
                return NotFound(new { message = $"QC inspection #{id} not found." });
            }

            var currentUserId = this.GetCurrentUserId() ?? 0;
            if (currentUserId > 0 && await IsTechnicianWhoWorkedOnOrderAsync(inspection.ServiceOrderId, currentUserId))
            {
                return BadRequest(new { message = "Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties)." });
            }

            var now = VietnamTime.Now;
            var incomingItems = request.Items ?? [];
            if (!request.OverwriteExisting && incomingItems.Count == 0)
            {
                return BadRequest(new { message = "items is required when overwriteExisting=false." });
            }

            if (request.OverwriteExisting)
            {
                var existingRows = await _db.QcChecklistItems
                    .Where(x => x.QcInspectionId == id)
                    .ToListAsync();
                if (existingRows.Count > 0)
                {
                    _db.QcChecklistItems.RemoveRange(existingRows);
                }

                foreach (var dto in incomingItems)
                {
                    var itemName = (dto.ItemName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(itemName))
                    {
                        return BadRequest(new { message = "Each checklist item requires itemName." });
                    }

                    var normalizedResult = NormalizeQcResult(dto.Result);
                    if (dto.Result is not null && normalizedResult is null)
                    {
                        return BadRequest(new { message = "result must be PASS, FAIL, N/A or empty." });
                    }

                    var row = new QcChecklistItem { QcInspectionId = id };
                    _db.QcChecklistItems.Add(row);

                    row.TemplateItemId = dto.TemplateItemId;
                    row.ItemName = itemName;
                    row.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
                    row.IsRequired = dto.IsRequired ?? true;
                    row.Result = normalizedResult;
                    row.FailNote = string.IsNullOrWhiteSpace(dto.FailNote) ? null : dto.FailNote.Trim();
                    row.PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim();
                    row.CheckedBy = dto.CheckedBy;
                    row.CheckedAt = dto.CheckedAt ?? (normalizedResult is null ? null : now);
                }
            }
            else
            {
                var itemIds = incomingItems
                    .Where(dto => dto.Id.HasValue && dto.Id.Value > 0)
                    .Select(dto => dto.Id!.Value)
                    .Distinct()
                    .ToList();

                var existingItemDict = itemIds.Count > 0
                    ? await _db.QcChecklistItems
                        .Where(x => x.QcInspectionId == id && itemIds.Contains(x.Id))
                        .ToDictionaryAsync(x => x.Id)
                    : new Dictionary<long, QcChecklistItem>();

                foreach (var dto in incomingItems)
                {
                    var itemName = (dto.ItemName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(itemName))
                    {
                        return BadRequest(new { message = "Each checklist item requires itemName." });
                    }

                    var normalizedResult = NormalizeQcResult(dto.Result);
                    if (dto.Result is not null && normalizedResult is null)
                    {
                        return BadRequest(new { message = "result must be PASS, FAIL, N/A or empty." });
                    }

                    QcChecklistItem row;
                    if (dto.Id.HasValue && dto.Id.Value > 0 && existingItemDict.TryGetValue(dto.Id.Value, out var foundRow))
                    {
                        row = foundRow;
                    }
                    else
                    {
                        row = new QcChecklistItem { QcInspectionId = id };
                        _db.QcChecklistItems.Add(row);
                    }

                    row.TemplateItemId = dto.TemplateItemId;
                    row.ItemName = itemName;
                    row.Category = string.IsNullOrWhiteSpace(dto.Category) ? null : dto.Category.Trim();
                    row.IsRequired = dto.IsRequired ?? true;
                    row.Result = normalizedResult;
                    row.FailNote = string.IsNullOrWhiteSpace(dto.FailNote) ? null : dto.FailNote.Trim();
                    row.PhotoUrl = string.IsNullOrWhiteSpace(dto.PhotoUrl) ? null : dto.PhotoUrl.Trim();
                    row.CheckedBy = dto.CheckedBy;
                    row.CheckedAt = dto.CheckedAt ?? (normalizedResult is null ? null : now);
                }
            }

            await _db.SaveChangesAsync();
            var summary = await UpdateQcInspectionSummaryAsync(inspection.Id);
            return Ok(summary);
        }

        [HttpPut("{id}/pass")]
        public async Task<IActionResult> Pass(long id, [FromBody] QcPassRequestDto request)
        {
            var inspection = await _db.QcInspections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (inspection == null) return NotFound();

            var inspectorId = request.InspectorId > 0 ? request.InspectorId : this.GetCurrentUserId() ?? 0;
            if (inspectorId > 0 && await IsTechnicianWhoWorkedOnOrderAsync(inspection.ServiceOrderId, inspectorId))
            {
                return BadRequest(new { message = "Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties)." });
            }

            var result = await _qcInspectionService.PassAsync(id, request);
            if (!result) return NotFound();
            return Ok(new { message = "QC Pass recorded and Service Order marked as READY." });
        }

        [HttpPut("{id}/fail")]
        public async Task<IActionResult> Fail(long id, [FromBody] QcFailRequestDto request)
        {
            var inspection = await _db.QcInspections.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (inspection == null) return NotFound();

            var inspectorId = request.InspectorId > 0 ? request.InspectorId : this.GetCurrentUserId() ?? 0;
            if (inspectorId > 0 && await IsTechnicianWhoWorkedOnOrderAsync(inspection.ServiceOrderId, inspectorId))
            {
                return BadRequest(new { message = "Kỹ thuật viên không được phép tự nghiệm thu (QC) công việc của chính mình (Separation of Duties)." });
            }

            var result = await _qcInspectionService.FailAsync(id, request);
            if (!result) return NotFound();
            return Ok(new { message = "QC Fail recorded and Service Order reverted to REPAIRING." });
        }

        private async Task<bool> IsTechnicianWhoWorkedOnOrderAsync(long serviceOrderId, long staffId)
        {
            if (serviceOrderId <= 0 || staffId <= 0) return false;

            var workedOnOrder = await _db.ServiceStaffAssignments
                .AsNoTracking()
                .AnyAsync(a => a.ServiceOrderDetail.ServiceOrderId == serviceOrderId && a.TechnicianId == staffId);

            if (!workedOnOrder)
            {
                workedOnOrder = await _db.ServiceOrderDetails
                    .AsNoTracking()
                    .AnyAsync(d => d.ServiceOrderId == serviceOrderId && d.TechnicianId == staffId);
            }

            return workedOnOrder;
        }

        private async Task<object> UpdateQcInspectionSummaryAsync(long inspectionId)
        {
            var inspection = await _db.QcInspections
                .Include(x => x.QcChecklistItems)
                .FirstOrDefaultAsync(x => x.Id == inspectionId);
            if (inspection is null)
            {
                return new { inspectionId, total = 0 };
            }

            var items = inspection.QcChecklistItems.ToList();
            var requiredItems = items.Where(x => x.IsRequired).ToList();
            var passOrNaRequired = requiredItems.Count(x =>
                string.Equals(x.Result, QcResultPass, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Result, QcResultNa, System.StringComparison.OrdinalIgnoreCase));
            var failRequired = requiredItems.Count(x =>
                string.Equals(x.Result, QcResultFail, System.StringComparison.OrdinalIgnoreCase));
            var checkedRequired = requiredItems.Count(x => !string.IsNullOrWhiteSpace(x.Result));

            decimal? score = null;
            if (requiredItems.Count > 0)
            {
                score = System.Math.Round((decimal)passOrNaRequired * 100m / requiredItems.Count, 2, System.MidpointRounding.AwayFromZero);
            }

            if (failRequired > 0)
            {
                inspection.Status = QcStatusFail;
                inspection.InspectedAt = VietnamTime.Now;
            }
            else if (requiredItems.Count > 0 && checkedRequired == requiredItems.Count)
            {
                inspection.Status = QcStatusPass;
                inspection.InspectedAt = VietnamTime.Now;
            }

            inspection.OverallScore = score;

            var serviceOrder = await _db.ServiceOrders.FirstOrDefaultAsync(x => x.Id == inspection.ServiceOrderId);
            if (serviceOrder != null)
            {
                if (string.Equals(inspection.Status, QcStatusFail, System.StringComparison.OrdinalIgnoreCase))
                {
                    serviceOrder.Qcstatus = ServiceOrderQcFail;
                }
                else if (string.Equals(inspection.Status, QcStatusPass, System.StringComparison.OrdinalIgnoreCase))
                {
                    serviceOrder.Qcstatus = ServiceOrderQcPass;
                    serviceOrder.QcverifiedAt = inspection.InspectedAt ?? VietnamTime.Now;
                }
                else
                {
                    serviceOrder.Qcstatus = ServiceOrderQcPending;
                }

                serviceOrder.QcinspectorId = inspection.QcStaffId;
                serviceOrder.UpdatedAt = VietnamTime.Now;
            }

            await _db.SaveChangesAsync();

            return new
            {
                inspectionId = inspection.Id,
                status = inspection.Status,
                overallScore = inspection.OverallScore,
                inspectedAt = inspection.InspectedAt,
                total = items.Count,
                required = requiredItems.Count,
                checkedRequired,
                failedRequired = failRequired,
                passedOrNaRequired = passOrNaRequired
            };
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






