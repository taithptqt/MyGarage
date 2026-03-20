using System.Collections.Generic;
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
    public class CommissionRuleController : ControllerBase
    {
        private readonly ICommissionRuleService _commissionRuleService;
        private readonly IMapper _mapper;

        public CommissionRuleController(ICommissionRuleService commissionRuleService, IMapper mapper)
        {
            _commissionRuleService = commissionRuleService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommissionRuleResponseDto>>> GetAll()
        {
            var items = await _commissionRuleService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CommissionRuleResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommissionRuleResponseDto>> GetById(long id)
        {
            var item = await _commissionRuleService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CommissionRuleResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CommissionRuleResponseDto>> Create([FromBody] CommissionRuleCreateDto itemDto)
        {
            var entity = _mapper.Map<CommissionRule>(itemDto);
            var createdItem = await _commissionRuleService.AddAsync(entity);
            var responseDto = _mapper.Map<CommissionRuleResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CommissionRuleResponseDto>> Update(long id, [FromBody] CommissionRuleUpdateDto itemDto)
        {
            var existingItem = await _commissionRuleService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _commissionRuleService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CommissionRuleResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _commissionRuleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





