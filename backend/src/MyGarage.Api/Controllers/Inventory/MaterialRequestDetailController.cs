using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [Route("api/v1/material-request-details")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class MaterialRequestDetailController : ControllerBase
    {
        private readonly IMaterialRequestDetailService _materialRequestDetailService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMaterialRequestDetailQueryService _queryService;

        public MaterialRequestDetailController(
            IMaterialRequestDetailService materialRequestDetailService,
            IServiceOrderService serviceOrderService,
            IMaterialRequestDetailQueryService queryService)
        {
            _materialRequestDetailService = materialRequestDetailService;
            _serviceOrderService = serviceOrderService;
            _queryService = queryService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<MaterialRequestDetailResponseDto>>> GetAll([FromQuery] long? serviceOrderId = null)
        {
            if (serviceOrderId.HasValue)
            {
                var items = await _queryService.GetIssuedByServiceOrderAsync(serviceOrderId.Value, HttpContext.RequestAborted);
                return Ok(_materialRequestDetailService.MapToDtos(items));
            }

            return Ok(await _materialRequestDetailService.GetAllDtosAsync());
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> GetById(long id)
        {
            var dto = await _materialRequestDetailService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<MaterialRequestDetailResponseDto>> Create([FromBody] MaterialRequestDetailCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || itemDto.RequestId <= 0)
                {
                    return this.BadRequestProblem("Thiếu RequestId hợp lệ để tạo chi tiết yêu cầu vật tư.");
                }

                var serviceOrderId = await ResolveServiceOrderIdFromRequestIdAsync(itemDto.RequestId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(serviceOrderId);
                }

                var responseDto = await _materialRequestDetailService.CreateDtoAsync(itemDto);
                return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
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

                var responseDto = await _materialRequestDetailService.UpdateDtoAsync(id, itemDto!, existingItem);
                if (responseDto == null)
                {
                    return NotFound();
                }

                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
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
                return this.BadRequestProblem(ex.Message);
            }
        }

        private Task<long> ResolveServiceOrderIdFromRequestIdAsync(long requestId)
            => _queryService.ResolveServiceOrderIdFromRequestIdAsync(requestId, HttpContext.RequestAborted);
    }
}






