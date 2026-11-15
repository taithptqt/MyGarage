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
    [Route("api/v1/expense-logs")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ExpenseLogController : ControllerBase
    {
        private readonly IExpenseLogService _expenseLogService;

        public ExpenseLogController(IExpenseLogService expenseLogService)
        {
            _expenseLogService = expenseLogService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<ExpenseLogResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _expenseLogService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _expenseLogService.MapToDtos(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ExpenseLogResponseDto>> GetById(long id)
        {
            var dto = await _expenseLogService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ExpenseLogResponseDto>> Create([FromBody] ExpenseLogCreateDto itemDto)
        {
            var responseDto = await _expenseLogService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ExpenseLogResponseDto>> Update(long id, [FromBody] ExpenseLogUpdateDto itemDto)
        {
            var responseDto = await _expenseLogService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






