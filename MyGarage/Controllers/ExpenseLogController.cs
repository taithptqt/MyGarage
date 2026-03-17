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
    public class ExpenseLogController : ControllerBase
    {
        private readonly IExpenseLogService _expenseLogService;
        private readonly IMapper _mapper;

        public ExpenseLogController(IExpenseLogService expenseLogService, IMapper mapper)
        {
            _expenseLogService = expenseLogService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExpenseLogResponseDto>>> GetAll()
        {
            var items = await _expenseLogService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ExpenseLogResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ExpenseLogResponseDto>> GetById(long id)
        {
            var item = await _expenseLogService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ExpenseLogResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ExpenseLogResponseDto>> Create([FromBody] ExpenseLogCreateDto itemDto)
        {
            var entity = _mapper.Map<ExpenseLog>(itemDto);
            var createdItem = await _expenseLogService.AddAsync(entity);
            var responseDto = _mapper.Map<ExpenseLogResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ExpenseLogResponseDto>> Update(long id, [FromBody] ExpenseLogUpdateDto itemDto)
        {
            var existingItem = await _expenseLogService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _expenseLogService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ExpenseLogResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _expenseLogService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





