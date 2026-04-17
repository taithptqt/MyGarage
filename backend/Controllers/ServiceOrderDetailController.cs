using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/service-order-details")]
    [ApiController]
    public class ServiceOrderDetailController : ControllerBase
    {
        private readonly IServiceOrderDetailService _serviceOrderDetailService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public ServiceOrderDetailController(
            IServiceOrderDetailService serviceOrderDetailService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _serviceOrderDetailService = serviceOrderDetailService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceOrderDetailResponseDto>>> GetAll([FromQuery] long? serviceOrderId = null)
        {
            IEnumerable<ServiceOrderDetail> items;
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                items = await _context.ServiceOrderDetails
                    .AsNoTracking()
                    .Where(detail => detail.ServiceOrderId == serviceOrderId.Value)
                    .OrderBy(detail => detail.Id)
                    .ToListAsync();
            }
            else
            {
                items = await _serviceOrderDetailService.GetAllAsync();
            }

            var dtos = _mapper.Map<IEnumerable<ServiceOrderDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> GetById(long id)
        {
            var item = await _serviceOrderDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<ServiceOrderDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Create([FromBody] ServiceOrderDetailCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || itemDto.ServiceOrderId <= 0)
                {
            return BadRequest(new { message = "Thiếu ServiceOrderId hợp lệ để tạo hạng mục service order." });
                }
                await _serviceOrderService.EnsureWorkshopEditableAsync(itemDto.ServiceOrderId);

                var entity = _mapper.Map<ServiceOrderDetail>(itemDto);
                EnsureNoDirectSupplementaryCreate(entity);
                var createdItem = await _serviceOrderDetailService.AddAsync(entity);
                var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ServiceOrderDetailResponseDto>> Update(long id, [FromBody] ServiceOrderDetailUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId > 0
                    ? itemDto.ServiceOrderId
                    : existingItem.ServiceOrderId;
                await _serviceOrderService.EnsureWorkshopEditableAsync(targetServiceOrderId);
                EnsureNoMutationForAppliedApprovedDetail(existingItem);

                var sourceSnapshot = CaptureSourceSnapshot(existingItem);
                _mapper.Map(itemDto, existingItem);
                RestoreSourceSnapshot(existingItem, sourceSnapshot);
                var updatedItem = await _serviceOrderDetailService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<ServiceOrderDetailResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _serviceOrderDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }
                await _serviceOrderService.EnsureWorkshopEditableAsync(existingItem.ServiceOrderId);
                EnsureNoMutationForAppliedApprovedDetail(existingItem);

                var result = await _serviceOrderDetailService.DeleteAsync(id);
                if (!result)
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

        private static void EnsureNoDirectSupplementaryCreate(ServiceOrderDetail entity)
        {
            var source = (entity.ServiceSource ?? string.Empty).Trim().ToUpperInvariant();
            var isSupplementarySource =
                source == nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN) ||
                source == nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER);

            if (entity.IsSupplementary || isSupplementarySource)
            {
                throw new InvalidOperationException("Không thể thêm trực tiếp dịch vụ phát sinh trên ServiceOrderDetails. Hãy tạo/duyệt báo giá bổ sung trước.");
            }
        }

        private static void EnsureNoMutationForAppliedApprovedDetail(ServiceOrderDetail entity)
        {
            if (entity == null)
            {
                return;
            }

            var hasAppliedQuotationLink = entity.QuotationDetailId.HasValue && entity.QuotationDetailId.Value > 0;
            var normalizedStatus = (entity.Status ?? string.Empty).Trim().ToUpperInvariant();
            var isApprovedFromQuotation =
                normalizedStatus == nameof(ServiceOrderDetailStatusCode.APPROVED) &&
                entity.SourceQuotationId.HasValue &&
                entity.SourceQuotationId.Value > 0;

            if (!hasAppliedQuotationLink && !isApprovedFromQuotation)
            {
                return;
            }

            throw new InvalidOperationException(
                    "Không cho phép sửa/xóa dịch vụ đã duyệt và đã đồng bộ vào SOD. Hãy tạo bản báo giá điều chỉnh (clone/supersede) để thay đổi.");
        }

        private static SourceSnapshot CaptureSourceSnapshot(ServiceOrderDetail existingItem)
        {
            if (existingItem == null)
            {
                return new SourceSnapshot();
            }

            return new SourceSnapshot
            {
                ServiceSource = NormalizeSource(existingItem.ServiceSource),
                IsSupplementary = existingItem.IsSupplementary,
                SupplementaryReasonCode = NormalizeReason(existingItem.SupplementaryReasonCode),
                SourceQuotationId = existingItem.SourceQuotationId,
                SupplementaryRequestedByStaffId = existingItem.SupplementaryRequestedByStaffId,
                SupplementaryRequestedAt = existingItem.SupplementaryRequestedAt,
                CustomerApprovedAt = existingItem.CustomerApprovedAt,
                QuotationDetailId = existingItem.QuotationDetailId
            };
        }

        private static void RestoreSourceSnapshot(ServiceOrderDetail existingItem, SourceSnapshot snapshot)
        {
            if (existingItem == null)
            {
                return;
            }

            existingItem.ServiceSource = snapshot.ServiceSource;
            existingItem.IsSupplementary = snapshot.IsSupplementary;
            existingItem.SupplementaryReasonCode = snapshot.SupplementaryReasonCode;
            existingItem.SourceQuotationId = snapshot.SourceQuotationId;
            existingItem.SupplementaryRequestedByStaffId = snapshot.SupplementaryRequestedByStaffId;
            existingItem.SupplementaryRequestedAt = snapshot.SupplementaryRequestedAt;
            existingItem.CustomerApprovedAt = snapshot.CustomerApprovedAt;
            existingItem.QuotationDetailId = snapshot.QuotationDetailId;
        }

        private static string NormalizeSource(string? source)
        {
            var normalized = (source ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_TECHNICIAN),
                nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER) => nameof(ServiceOrderDetailServiceSourceCode.SUPPLEMENTARY_CUSTOMER),
                _ => nameof(ServiceOrderDetailServiceSourceCode.INITIAL)
            };
        }

        private static string? NormalizeReason(string? reason)
        {
            var normalized = (reason ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING) => nameof(ServiceOrderDetailSupplementaryReasonCode.TECH_FINDING),
                nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST) => nameof(ServiceOrderDetailSupplementaryReasonCode.CUSTOMER_REQUEST),
                _ => null
            };
        }

        private sealed class SourceSnapshot
        {
            public string ServiceSource { get; set; } = nameof(ServiceOrderDetailServiceSourceCode.INITIAL);
            public bool IsSupplementary { get; set; }
            public string? SupplementaryReasonCode { get; set; }
            public long? SourceQuotationId { get; set; }
            public long? SupplementaryRequestedByStaffId { get; set; }
            public DateTime? SupplementaryRequestedAt { get; set; }
            public DateTime? CustomerApprovedAt { get; set; }
            public long? QuotationDetailId { get; set; }
        }
    }
}
