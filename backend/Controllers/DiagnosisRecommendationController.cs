using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MyGarage.Common;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Hubs;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/diagnosis-recommendations")]
    [Route("api/diagnose-recommendations")]
    [ApiController]
    public class DiagnosisRecommendationController : ControllerBase
    {
        private const string EvidenceRequiredMessage = "Mỗi dịch vụ mới phải có ít nhất 1 ảnh hiện trạng trước khi lưu.";

        private readonly IDiagnosisRecommendationService _diagnosisRecommendationService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;
        private readonly IHubContext<ChatHub> _hubContext;

        public DiagnosisRecommendationController(
            IDiagnosisRecommendationService diagnosisRecommendationService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper,
            IHubContext<ChatHub> hubContext)
        {
            _diagnosisRecommendationService = diagnosisRecommendationService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiagnosisRecommendationResponseDto>>> GetAll(
            [FromQuery] long? diagnosisId,
            [FromQuery] long? diagnosisAssignmentId)
        {
            var items = await _diagnosisRecommendationService.GetAllAsync();
            if (diagnosisAssignmentId.HasValue && diagnosisAssignmentId.Value > 0)
            {
                items = items.Where(x => x.DiagnosisAssignmentId == diagnosisAssignmentId.Value).ToList();
            }
            else if (diagnosisId.HasValue && diagnosisId.Value > 0)
            {
                items = items.Where(x => x.DiagnosisId == diagnosisId.Value).ToList();
            }

            var dtos = _mapper.Map<IEnumerable<DiagnosisRecommendationResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("pending-supplementary")]
        public async Task<ActionResult<IEnumerable<PendingSupplementaryProposalResponseDto>>> GetPendingSupplementary(
            [FromQuery] long? serviceOrderId = null,
            [FromQuery] long? branchId = null)
        {
            var orderQuery = _context.ServiceOrders.AsNoTracking().AsQueryable();
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                orderQuery = orderQuery.Where(x => x.Id == serviceOrderId.Value);
            }

            if (branchId.HasValue && branchId.Value > 0)
            {
                orderQuery = orderQuery.Where(x => x.ExecuteBranchId == branchId.Value || x.OriginBranchId == branchId.Value);
            }

            var candidateOrderIds = await orderQuery
                .Select(x => x.Id)
                .Distinct()
                .ToListAsync();
            if (candidateOrderIds.Count == 0)
            {
                return Ok(Array.Empty<PendingSupplementaryProposalResponseDto>());
            }

            var latestDiagnosisByOrder = await _context.Diagnoses.AsNoTracking()
                .Where(x => candidateOrderIds.Contains(x.ServiceOrderId))
                .Where(x => x.Status != nameof(DiagnosisStatus.CANCELED))
                .GroupBy(x => x.ServiceOrderId)
                .Select(g => g
                    .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
                    .ThenByDescending(x => x.Id)
                    .Select(x => new { x.ServiceOrderId, x.Id })
                    .FirstOrDefault())
                .Where(x => x != null)
                .ToListAsync();

            var latestDiagnosisIds = latestDiagnosisByOrder
                .Select(x => x!.Id)
                .Distinct()
                .ToList();
            if (latestDiagnosisIds.Count == 0)
            {
                return Ok(Array.Empty<PendingSupplementaryProposalResponseDto>());
            }

            var orderIdByDiagnosisId = latestDiagnosisByOrder
                .ToDictionary(x => x!.Id, x => x!.ServiceOrderId);

            var recommendations = await _context.DiagnosisRecommendations.AsNoTracking()
                .Where(x => latestDiagnosisIds.Contains(x.DiagnosisId))
                .Where(x => x.RecommendationStatus != nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL))
                .Select(x => new
                {
                    x.Id,
                    x.DiagnosisId,
                    x.DiagnosisAssignmentId,
                    ProposalAt = x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue
                })
                .ToListAsync();
            if (recommendations.Count == 0)
            {
                return Ok(Array.Empty<PendingSupplementaryProposalResponseDto>());
            }

            var recommendationAssignmentIds = recommendations
                .Where(x => x.DiagnosisAssignmentId.HasValue && x.DiagnosisAssignmentId.Value > 0)
                .Select(x => x.DiagnosisAssignmentId!.Value)
                .Distinct()
                .ToList();

            var supplementaryQuotations = await _context.Quotations.AsNoTracking()
                .Where(q => candidateOrderIds.Contains(q.ServiceOrderId))
                .Where(q =>
                    q.VersionNo > 1 &&
                    q.Status != nameof(QuotationStatusCode.SUPERSEDED) &&
                    q.Status != nameof(QuotationStatusCode.CANCELED))
                .Where(q =>
                    (q.DiagnosisAssignmentId.HasValue && recommendationAssignmentIds.Contains(q.DiagnosisAssignmentId.Value)) ||
                    (!q.DiagnosisAssignmentId.HasValue && latestDiagnosisIds.Contains(q.DiagnosisId ?? 0)))
                .Select(q => new
                {
                    q.DiagnosisAssignmentId,
                    q.DiagnosisId,
                    QuotedAt = q.CreatedAt ?? DateTime.MinValue
                })
                .ToListAsync();

            var latestSupplementaryQuotedAtByAssignmentId = supplementaryQuotations
                .Where(x => x.DiagnosisAssignmentId.HasValue && x.DiagnosisAssignmentId.Value > 0)
                .GroupBy(x => x.DiagnosisAssignmentId!.Value)
                .ToDictionary(x => x.Key, x => x.Max(y => y.QuotedAt));

            var latestSupplementaryQuotedAtByDiagnosisId = supplementaryQuotations
                .Where(x => x.DiagnosisId.HasValue && x.DiagnosisId.Value > 0)
                .GroupBy(x => x.DiagnosisId!.Value)
                .ToDictionary(x => x.Key, x => x.Max(y => y.QuotedAt));

            var pendingGroups = recommendations
                .Where(rec =>
                {
                    DateTime quotedAt;
                    var hasQuotedAt = rec.DiagnosisAssignmentId.HasValue && rec.DiagnosisAssignmentId.Value > 0
                        ? latestSupplementaryQuotedAtByAssignmentId.TryGetValue(rec.DiagnosisAssignmentId.Value, out quotedAt)
                        : latestSupplementaryQuotedAtByDiagnosisId.TryGetValue(rec.DiagnosisId, out quotedAt);
                    if (!hasQuotedAt)
                    {
                        return true;
                    }

                    return rec.ProposalAt > quotedAt;
                })
                .GroupBy(rec => orderIdByDiagnosisId.TryGetValue(rec.DiagnosisId, out var soId) ? soId : 0L)
                .Where(group => group.Key > 0)
                .Select(group => new PendingSupplementaryProposalResponseDto
                {
                    ServiceOrderId = group.Key,
                    PendingCount = group.Count(),
                    LastRecommendationAt = group.Max(x => (DateTime?)x.ProposalAt)
                })
                .OrderByDescending(x => x.LastRecommendationAt ?? DateTime.MinValue)
                .ToList();

            return Ok(pendingGroups);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<DiagnosisRecommendationResponseDto>> GetById(long id)
        {
            var item = await _diagnosisRecommendationService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<DiagnosisRecommendationResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DiagnosisRecommendationResponseDto>> Create([FromBody] DiagnosisRecommendationCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || (!itemDto.DiagnosisAssignmentId.HasValue && !itemDto.DiagnosisId.HasValue))
                {
                    return BadRequest(new { message = "Thiếu DiagnosisAssignmentId hoặc DiagnosisId hợp lệ để tạo kiến nghị kỹ thuật." });
                }

                var diagnosisLink = await ResolveDiagnosisLinkAsync(itemDto.DiagnosisAssignmentId, itemDto.DiagnosisId);
                if (!diagnosisLink.HasValue)
                {
                    return BadRequest(new { message = "Không tìm thấy vòng phân công chuẩn đoán hợp lệ." });
                }

                var serviceOrderId = await ResolveServiceOrderIdFromDiagnosisIdAsync(diagnosisLink.Value.DiagnosisId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                var entity = _mapper.Map<DiagnosisRecommendation>(itemDto);
                entity.DiagnosisId = diagnosisLink.Value.DiagnosisId;
                entity.DiagnosisAssignmentId = diagnosisLink.Value.DiagnosisAssignmentId;
                entity.RecommendationStatus = string.IsNullOrWhiteSpace(entity.RecommendationStatus)
                    ? nameof(RecommendationLifecycleStatusCode.ACTIVE)
                    : entity.RecommendationStatus;

                if (RequiresEvidenceForServiceRecommendation(entity) && !HasEvidenceMedia(entity.EvidenceMediaUrls))
                {
                    return BadRequest(new { message = EvidenceRequiredMessage });
                }

                var createdItem = await _diagnosisRecommendationService.AddAsync(entity);
                await NotifySupplementaryProposalToStaffAsync(createdItem.DiagnosisId, createdItem.Id, "CREATED", createdItem.CreatedBy);
                var responseDto = _mapper.Map<DiagnosisRecommendationResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<DiagnosisRecommendationResponseDto>> Update(long id, [FromBody] DiagnosisRecommendationUpdateDto itemDto)
        {
            try
            {
                if (itemDto == null)
                {
                    return BadRequest(new { message = "Dữ liệu cập nhật không hợp lệ." });
                }

                var existingItem = await _diagnosisRecommendationService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var diagnosisLink = await ResolveDiagnosisLinkAsync(
                    itemDto.DiagnosisAssignmentId ?? existingItem.DiagnosisAssignmentId,
                    itemDto.DiagnosisId ?? existingItem.DiagnosisId);
                if (!diagnosisLink.HasValue)
                {
                    return BadRequest(new { message = "Không tìm thấy vòng phân công chuẩn đoán hợp lệ." });
                }

                var serviceOrderId = await ResolveServiceOrderIdFromDiagnosisIdAsync(diagnosisLink.Value.DiagnosisId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                var originalQuantity = existingItem.Quantity;
                _mapper.Map(itemDto, existingItem);
                existingItem.DiagnosisId = diagnosisLink.Value.DiagnosisId;
                existingItem.DiagnosisAssignmentId = diagnosisLink.Value.DiagnosisAssignmentId;

                if (RequiresEvidenceForServiceRecommendation(existingItem) && !HasEvidenceMedia(existingItem.EvidenceMediaUrls))
                {
                    return BadRequest(new { message = EvidenceRequiredMessage });
                }

                var updatedItem = await _diagnosisRecommendationService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }
                await NotifySupplementaryProposalToStaffAsync(updatedItem.DiagnosisId, updatedItem.Id, "UPDATED", updatedItem.UpdatedBy);

                var hasQuantityChanged = Math.Abs(updatedItem.Quantity - originalQuantity) > 0.000001m;
                if (hasQuantityChanged)
                {
                    await SyncChildMaterialQuantitiesByBomAsync(updatedItem);
                }

                var responseDto = _mapper.Map<DiagnosisRecommendationResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _diagnosisRecommendationService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var serviceOrderId = existingItem.DiagnosisAssignmentId.HasValue
                    ? await ResolveServiceOrderIdFromAssignmentIdAsync(existingItem.DiagnosisAssignmentId.Value)
                    : await ResolveServiceOrderIdFromDiagnosisIdAsync(existingItem.DiagnosisId);

                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                existingItem.RecommendationStatus = nameof(RecommendationLifecycleStatusCode.SOFT_DELETED_BY_TL);
                existingItem.SoftDeletedAt = VietnamTime.Now;
                existingItem.SoftDeletedBy = existingItem.UpdatedBy;

                var updatedItem = await _diagnosisRecommendationService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<long> ResolveServiceOrderIdFromDiagnosisIdAsync(long diagnosisId)
        {
            if (diagnosisId <= 0)
            {
                return 0;
            }

            return await _context.Diagnoses
                .AsNoTracking()
                .Where(x => x.Id == diagnosisId)
                .Select(x => x.ServiceOrderId)
                .FirstOrDefaultAsync();
        }

        private async Task<long> ResolveServiceOrderIdFromAssignmentIdAsync(long diagnosisAssignmentId)
        {
            if (diagnosisAssignmentId <= 0)
            {
                return 0;
            }

            return await _context.DiagnosisAssignments
                .AsNoTracking()
                .Where(x => x.Id == diagnosisAssignmentId)
                .Join(
                    _context.Diagnoses.AsNoTracking(),
                    assignment => assignment.DiagnosisId,
                    diagnosis => diagnosis.Id,
                    (_, diagnosis) => diagnosis.ServiceOrderId)
                .FirstOrDefaultAsync();
        }

        private async Task<(long DiagnosisId, long DiagnosisAssignmentId)?> ResolveDiagnosisLinkAsync(long? diagnosisAssignmentId, long? diagnosisId)
        {
            if (diagnosisAssignmentId.HasValue && diagnosisAssignmentId.Value > 0)
            {
                var fromAssignment = await _context.DiagnosisAssignments
                    .AsNoTracking()
                    .Where(x => x.Id == diagnosisAssignmentId.Value)
                    .Select(x => new { x.DiagnosisId, x.Id })
                    .FirstOrDefaultAsync();
                if (fromAssignment == null)
                {
                    return null;
                }

                if (diagnosisId.HasValue && diagnosisId.Value > 0 && diagnosisId.Value != fromAssignment.DiagnosisId)
                {
                    return null;
                }

                return (fromAssignment.DiagnosisId, fromAssignment.Id);
            }

            if (!diagnosisId.HasValue || diagnosisId.Value <= 0)
            {
                return null;
            }

            var latestAssignmentId = await _context.DiagnosisAssignments
                .AsNoTracking()
                .Where(x => x.DiagnosisId == diagnosisId.Value)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();

            if (!latestAssignmentId.HasValue || latestAssignmentId.Value <= 0)
            {
                return null;
            }

            return (diagnosisId.Value, latestAssignmentId.Value);
        }

        private static bool RequiresEvidenceForServiceRecommendation(DiagnosisRecommendation recommendation)
        {
            if (recommendation == null)
            {
                return false;
            }

            if (recommendation.ParentRecommendationId.HasValue && recommendation.ParentRecommendationId.Value > 0)
            {
                return false;
            }

            if (recommendation.StandardServiceId.HasValue && recommendation.StandardServiceId.Value > 0)
            {
                return true;
            }

            var normalizedItemType = (recommendation.ItemType ?? string.Empty).Trim().ToUpperInvariant();
            return normalizedItemType is "SERVICE" or "STANDARD_SERVICE" or "LABOR";
        }

        private static bool HasEvidenceMedia(string? evidenceMediaUrls)
        {
            if (string.IsNullOrWhiteSpace(evidenceMediaUrls))
            {
                return false;
            }

            var trimmed = evidenceMediaUrls.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                return trimmed
                    .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Any(x => !string.IsNullOrWhiteSpace(x));
            }

            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(element.GetString()))
                    {
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
        }

        private async Task NotifySupplementaryProposalToStaffAsync(long diagnosisId, long recommendationId, string action, long? actorStaffId)
        {
            if (diagnosisId <= 0 || recommendationId <= 0)
            {
                return;
            }

            var target = await _context.Diagnoses.AsNoTracking()
                .Where(x => x.Id == diagnosisId)
                .Join(
                    _context.ServiceOrders.AsNoTracking(),
                    diagnosis => diagnosis.ServiceOrderId,
                    serviceOrder => serviceOrder.Id,
                    (diagnosis, serviceOrder) => new
                    {
                        ServiceOrderId = serviceOrder.Id,
                        serviceOrder.Code,
                        serviceOrder.ExecuteBranchId,
                        serviceOrder.OriginBranchId,
                        serviceOrder.ReceptionistId,
                        serviceOrder.ServiceAdvisorId
                    })
                .FirstOrDefaultAsync();

            if (target == null)
            {
                return;
            }

            var recipientStaffIds = new HashSet<long>();
            if (target.ReceptionistId.HasValue && target.ReceptionistId.Value > 0)
            {
                recipientStaffIds.Add(target.ReceptionistId.Value);
            }

            if (target.ServiceAdvisorId.HasValue && target.ServiceAdvisorId.Value > 0)
            {
                recipientStaffIds.Add(target.ServiceAdvisorId.Value);
            }

            if (actorStaffId.HasValue && actorStaffId.Value > 0)
            {
                recipientStaffIds.Remove(actorStaffId.Value);
            }

            if (recipientStaffIds.Count == 0)
            {
                return;
            }

            var payload = new
            {
                eventType = "SUPPLEMENTARY_PROPOSAL",
                action = (action ?? "UPDATED").Trim().ToUpperInvariant(),
                serviceOrderId = target.ServiceOrderId,
                serviceOrderCode = target.Code,
                diagnosisId,
                recommendationId,
                executeBranchId = target.ExecuteBranchId,
                originBranchId = target.OriginBranchId,
                occurredAt = VietnamTime.Now
            };

            foreach (var staffId in recipientStaffIds)
            {
                await _hubContext.Clients.Group($"User_{staffId}")
                    .SendAsync("SupplementaryProposalUpdated", payload);
            }
        }

        private async Task SyncChildMaterialQuantitiesByBomAsync(DiagnosisRecommendation parentRecommendation)
        {
            if (parentRecommendation == null)
            {
                return;
            }

            if (parentRecommendation.ParentRecommendationId.HasValue && parentRecommendation.ParentRecommendationId.Value > 0)
            {
                return;
            }

            if (!IsServiceRecommendation(parentRecommendation))
            {
                return;
            }

            var standardServiceId = parentRecommendation.StandardServiceId ?? 0;
            if (standardServiceId <= 0)
            {
                return;
            }

            var vehicleModelId = await _context.Diagnoses
                .AsNoTracking()
                .Where(x => x.Id == parentRecommendation.DiagnosisId)
                .Join(
                    _context.ServiceOrders.AsNoTracking(),
                    diagnosis => diagnosis.ServiceOrderId,
                    serviceOrder => serviceOrder.Id,
                    (diagnosis, serviceOrder) => serviceOrder.VehicleId)
                .Join(
                    _context.Vehicles.AsNoTracking(),
                    vehicleId => vehicleId,
                    vehicle => vehicle.Id,
                    (_, vehicle) => vehicle.ModelId)
                .FirstOrDefaultAsync();

            if (vehicleModelId <= 0)
            {
                return;
            }

            var bomByProductId = await _context.ServiceBoms
                .AsNoTracking()
                .Where(x => x.ServiceId == standardServiceId && x.VehicleModelId == vehicleModelId)
                .GroupBy(x => x.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Quantity = g.OrderByDescending(x => x.UpdateAt).ThenByDescending(x => x.Id).Select(x => x.Quantity).FirstOrDefault()
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity);

            if (bomByProductId.Count == 0)
            {
                return;
            }

            var children = await _context.DiagnosisRecommendations
                .Where(x => x.ParentRecommendationId == parentRecommendation.Id)
                .ToListAsync();

            if (children.Count == 0)
            {
                return;
            }

            var parentQty = parentRecommendation.Quantity <= 0 ? 1m : parentRecommendation.Quantity;
            var hasAnyChange = false;

            foreach (var child in children)
            {
                if (child.InventoryItemId == null || child.InventoryItemId <= 0)
                {
                    continue;
                }

                if (IsSoftDeletedRecommendation(child.RecommendationStatus))
                {
                    continue;
                }

                if (!bomByProductId.TryGetValue(child.InventoryItemId.Value, out var bomQuantity) || bomQuantity <= 0)
                {
                    continue;
                }

                var targetQty = Math.Round(bomQuantity * parentQty, 3, MidpointRounding.AwayFromZero);
                if (targetQty <= 0)
                {
                    targetQty = 1m;
                }

                if (Math.Abs(child.Quantity - targetQty) <= 0.000001m)
                {
                    continue;
                }

                child.Quantity = targetQty;
                child.UpdatedAt = VietnamTime.Now;
                child.UpdatedBy = parentRecommendation.UpdatedBy;
                hasAnyChange = true;
            }

            if (hasAnyChange)
            {
                await _context.SaveChangesAsync();
            }
        }

        private static bool IsServiceRecommendation(DiagnosisRecommendation recommendation)
        {
            var token = string.IsNullOrWhiteSpace(recommendation?.ItemType)
                ? string.Empty
                : recommendation.ItemType.Trim().ToUpperInvariant();

            return token.Contains("SERVICE") || token.Contains("LABOR");
        }

        private static bool IsSoftDeletedRecommendation(string? recommendationStatus)
        {
            if (string.IsNullOrWhiteSpace(recommendationStatus))
            {
                return false;
            }

            return recommendationStatus.Trim().StartsWith("SOFT_DELETED", StringComparison.OrdinalIgnoreCase);
        }
    }
}
