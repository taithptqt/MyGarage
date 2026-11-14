using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/expense-logs")]
    [Route("api/v1/expense-logs")]
    [ApiController]
    [Authorize(Roles = "Staff")]
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
        public async Task<ActionResult<PagedEnvelope<ExpenseLogResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _expenseLogService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _mapper.Map<IEnumerable<ExpenseLogResponseDto>>(paged));
            return Ok(envelope);
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






