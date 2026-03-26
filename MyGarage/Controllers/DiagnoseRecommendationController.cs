using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagnoseRecommendationController : ControllerBase
    {
        private readonly IDiagnoseRecommendationService _diagnoseRecommendationService;
        private readonly IMapper _mapper;

        public DiagnoseRecommendationController(IDiagnoseRecommendationService diagnoseRecommendationService, IMapper mapper)
        {
            _diagnoseRecommendationService = diagnoseRecommendationService;
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
            var entity = _mapper.Map<DiagnoseRecommendation>(itemDto);
            var createdItem = await _diagnoseRecommendationService.AddAsync(entity);
            var responseDto = _mapper.Map<DiagnoseRecommendationResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<DiagnoseRecommendationResponseDto>> Update(long id, [FromBody] DiagnoseRecommendationUpdateDto itemDto)
        {
            var existingItem = await _diagnoseRecommendationService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _diagnoseRecommendationService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}
