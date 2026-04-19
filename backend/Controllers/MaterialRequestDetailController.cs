using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Controllers
{
    [Route("api/material-request-details")]
    [ApiController]
    public class MaterialRequestDetailController : ControllerBase
    {
        private readonly IMaterialRequestDetailService _materialRequestDetailService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public MaterialRequestDetailController(
            IMaterialRequestDetailService materialRequestDetailService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _materialRequestDetailService = materialRequestDetailService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MaterialRequestDetailResponseDto>>> GetAll([FromQuery] long? serviceOrderId = null)
        {
            IEnumerable<MaterialRequestDetail> items;
            if (serviceOrderId.HasValue)
            {
                items = await _context.MaterialRequestDetails
                    .Include(d => d.Product)
                    .Where(d => d.Request.ServiceOrderId == serviceOrderId.Value && (d.IssuedQuantity ?? 0) > 0)
                    .ToListAsync();
            }
            else
            {
                items = await _materialRequestDetailService.GetAllAsync();
            }
            var dtos = _mapper.Map<IEnumerable<MaterialRequestDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> GetById(long id)
        {
            var item = await _materialRequestDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<MaterialRequestDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> Create([FromBody] MaterialRequestDetailCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || itemDto.RequestId <= 0)
                {
                    return BadRequest(new { message = "Thiếu RequestId hợp lệ để tạo chi tiết yêu cầu vật tư." });
                }

                var serviceOrderId = await ResolveServiceOrderIdFromRequestIdAsync(itemDto.RequestId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(serviceOrderId);
                }

                var entity = _mapper.Map<MaterialRequestDetail>(itemDto);
                var createdItem = await _materialRequestDetailService.AddAsync(entity);
                var responseDto = _mapper.Map<MaterialRequestDetailResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> Update(long id, [FromBody] MaterialRequestDetailUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _materialRequestDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var requestId = itemDto?.RequestId > 0 ? itemDto.RequestId : existingItem.RequestId;
                var serviceOrderId = await ResolveServiceOrderIdFromRequestIdAsync(requestId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(serviceOrderId);
                }

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _materialRequestDetailService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<MaterialRequestDetailResponseDto>(updatedItem);
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
                var existingItem = await _materialRequestDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var serviceOrderId = await ResolveServiceOrderIdFromRequestIdAsync(existingItem.RequestId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(serviceOrderId);
                }

                var result = await _materialRequestDetailService.DeleteAsync(id);
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

        private async Task<long> ResolveServiceOrderIdFromRequestIdAsync(long requestId)
        {
            if (requestId <= 0)
            {
                return 0;
            }

            var serviceOrderId = await _context.MaterialRequests
                .AsNoTracking()
                .Where(request => request.Id == requestId)
                .Select(request => request.ServiceOrderId)
                .FirstOrDefaultAsync();

            if (!serviceOrderId.HasValue || serviceOrderId.Value <= 0)
            {
                return 0;
            }

            return serviceOrderId.Value;
        }
    }
}






