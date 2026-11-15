using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/material-returns")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class MaterialReturnController : ControllerBase
    {
        private readonly IMaterialReturnService _materialReturnService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IStockAlertSyncService _stockAlertSyncService;

        public MaterialReturnController(
            IMaterialReturnService materialReturnService,
            IServiceOrderService serviceOrderService,
            IStockAlertSyncService stockAlertSyncService)
        {
            _materialReturnService = materialReturnService;
            _serviceOrderService = serviceOrderService;
            _stockAlertSyncService = stockAlertSyncService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<MaterialReturnResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _materialReturnService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                _materialReturnService.MapToDtos);
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<MaterialReturnResponseDto>> GetById(long id)
        {
            var dto = await _materialReturnService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<MaterialReturnResponseDto>> Create([FromBody] MaterialReturnCreateDto itemDto)
        {
            try
            {
                var request = itemDto;
                if (request == null)
                {
                    return this.BadRequestProblem("Dữ liệu trả vật tư không hợp lệ.");
                }

                if (request.ServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(request.ServiceOrderId.Value);
                }

                var responseDto = await _materialReturnService.ProcessReturnDtoAsync(request);
                await _stockAlertSyncService.RebuildStockAlertsAsync(this.GetCurrentUserId());
                return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<MaterialReturnResponseDto>> Update(long id, [FromBody] MaterialReturnUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _materialReturnService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId;
                if (targetServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(targetServiceOrderId.Value);
                }

                var responseDto = await _materialReturnService.UpdateDtoAsync(id, itemDto!, existingItem);
                if (responseDto == null)
                {
                    return NotFound();
                }

                await _stockAlertSyncService.RebuildStockAlertsAsync(this.GetCurrentUserId());
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
                var existingItem = await _materialReturnService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                if (existingItem.ServiceOrderId > 0)
                {
                    await _serviceOrderService.EnsurePartsEditableAsync(existingItem.ServiceOrderId.Value);
                }

                var result = await _materialReturnService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                await _stockAlertSyncService.RebuildStockAlertsAsync(this.GetCurrentUserId());
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }
    }
}




