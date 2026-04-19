using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/qc-inspections")]
    [ApiController]
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
        public async Task<ActionResult<IEnumerable<QcInspectionResponseDto>>> GetAll()
        {
            var items = await _qcInspectionService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<QcInspectionResponseDto>>(items);
            return Ok(dtos);
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

                QcChecklistItem row;
                if (!request.OverwriteExisting && dto.Id.HasValue && dto.Id.Value > 0)
                {
                    row = await _db.QcChecklistItems
                        .FirstOrDefaultAsync(x => x.Id == dto.Id.Value && x.QcInspectionId == id)
                        ?? new QcChecklistItem { QcInspectionId = id };

                    if (row.Id == 0)
                    {
                        _db.QcChecklistItems.Add(row);
                    }
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

            await _db.SaveChangesAsync();
            var summary = await UpdateQcInspectionSummaryAsync(inspection.Id);
            return Ok(summary);
        }

        [HttpPut("{id}/pass")]
        public async Task<IActionResult> Pass(long id, [FromBody] QcPassRequestDto request)
        {
            var result = await _qcInspectionService.PassAsync(id, request);
            if (!result) return NotFound();
            return Ok(new { message = "QC Pass recorded and Service Order marked as READY." });
        }

        [HttpPut("{id}/fail")]
        public async Task<IActionResult> Fail(long id, [FromBody] QcFailRequestDto request)
        {
            var result = await _qcInspectionService.FailAsync(id, request);
            if (!result) return NotFound();
            return Ok(new { message = "QC Fail recorded and Service Order reverted to REPAIRING." });
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






