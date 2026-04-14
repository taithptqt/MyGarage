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
    [Route("api/diagnose-recommendations")]
    [ApiController]
    public class DiagnoseRecommendationController : ControllerBase
    {
        private readonly IDiagnoseRecommendationService _diagnoseRecommendationService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public DiagnoseRecommendationController(
            IDiagnoseRecommendationService diagnoseRecommendationService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _diagnoseRecommendationService = diagnoseRecommendationService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DiagnoseRecommendationResponseDto>>> GetAll([FromQuery] long? diagnoseId)
        {
            var items = await _diagnoseRecommendationService.GetAllAsync();
            if (diagnoseId.HasValue)
            {
                items = items.Where(x => x.DiagnoseId == diagnoseId.Value).ToList();
            }

            var dtos = _mapper.Map<IEnumerable<DiagnoseRecommendationResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DiagnoseRecommendationResponseDto>> GetById(long id)
        {
            var item = await _diagnoseRecommendationService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<DiagnoseRecommendationResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<DiagnoseRecommendationResponseDto>> Create([FromBody] DiagnoseRecommendationCreateDto itemDto)
        {
            try
            {
                if (itemDto == null || itemDto.DiagnoseId <= 0)
                {
                    return BadRequest(new { message = "Thiếu DiagnoseId hợp lệ để tạo kiến nghị kỹ thuật." });
                }

                var serviceOrderId = await ResolveServiceOrderIdFromDiagnoseIdAsync(itemDto.DiagnoseId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                var entity = _mapper.Map<DiagnoseRecommendation>(itemDto);
                var createdItem = await _diagnoseRecommendationService.AddAsync(entity);
                var responseDto = _mapper.Map<DiagnoseRecommendationResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DiagnoseRecommendationResponseDto>> Update(long id, [FromBody] DiagnoseRecommendationUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _diagnoseRecommendationService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var diagnoseId = itemDto?.DiagnoseId > 0 ? itemDto.DiagnoseId : existingItem.DiagnoseId;
                var serviceOrderId = await ResolveServiceOrderIdFromDiagnoseIdAsync(diagnoseId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _diagnoseRecommendationService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<DiagnoseRecommendationResponseDto>(updatedItem);
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
                var existingItem = await _diagnoseRecommendationService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var serviceOrderId = await ResolveServiceOrderIdFromDiagnoseIdAsync(existingItem.DiagnoseId);
                if (serviceOrderId > 0)
                {
                    await _serviceOrderService.EnsureQuoteAndPartsEditableAsync(serviceOrderId);
                }

                var result = await _diagnoseRecommendationService.DeleteAsync(id);
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

        private async Task<long> ResolveServiceOrderIdFromDiagnoseIdAsync(long diagnoseId)
        {
            if (diagnoseId <= 0)
            {
                return 0;
            }

            return await _context.Diagnoses
                .AsNoTracking()
                .Where(x => x.Id == diagnoseId)
                .Select(x => x.ServiceOrderId)
                .FirstOrDefaultAsync();
        }
    }
}

