using System;
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
using MyGarage.Enums;

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

                if (IsSupplementaryDetail(itemDto.ServiceSource, itemDto.IsSupplementary, itemDto.SupplementaryReasonCode))
                {
                    return BadRequest(new
                    {
                        message = "Dịch vụ phát sinh phải đi qua báo giá bổ sung (Quotation #2), không được tạo trực tiếp trên ServiceOrderDetails."
                    });
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(itemDto.ServiceOrderId);

                var entity = _mapper.Map<ServiceOrderDetail>(itemDto);
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

                var targetServiceSource = itemDto?.ServiceSource ?? existingItem.ServiceSource;
                var targetIsSupplementary = itemDto?.IsSupplementary ?? existingItem.IsSupplementary;
                var targetSupplementaryReasonCode = itemDto?.SupplementaryReasonCode ?? existingItem.SupplementaryReasonCode;
                if (IsSupplementaryDetail(targetServiceSource, targetIsSupplementary, targetSupplementaryReasonCode))
                {
                    return BadRequest(new
                    {
                        message = "Dịch vụ phát sinh chỉ được điều chỉnh qua luồng báo giá bổ sung (Quotation #2)."
                    });
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(targetServiceOrderId);

                _mapper.Map(itemDto, existingItem);
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

                if (IsSupplementaryDetail(existingItem.ServiceSource, existingItem.IsSupplementary, existingItem.SupplementaryReasonCode))
                {
                    return BadRequest(new
                    {
                        message = "Dịch vụ phát sinh chỉ được hủy qua luồng báo giá bổ sung (Quotation #2)."
                    });
                }

                await _serviceOrderService.EnsureWorkshopEditableAsync(existingItem.ServiceOrderId);

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

        private static bool IsSupplementaryDetail(string? serviceSource, bool? isSupplementary, string? supplementaryReasonCode)
        {
            if (isSupplementary == true)
            {
                return true;
            }

            var normalizedSource = NormalizeToken(serviceSource);
            if (normalizedSource == nameof(ServiceOrderDetailSourceCode.SUPPLEMENTARY_TECHNICIAN) ||
                normalizedSource == nameof(ServiceOrderDetailSourceCode.SUPPLEMENTARY_CUSTOMER))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(NormalizeToken(supplementaryReasonCode));
        }

        private static string NormalizeToken(string? raw)
        {
            return string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : raw.Trim().ToUpperInvariant().Replace("-", "_").Replace(" ", "_");
        }
    }
}






