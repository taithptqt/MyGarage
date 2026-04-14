using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/expense-logs")]
    [ApiController]
    public class ExpenseLogController : ControllerBase
    {
        private readonly IExpenseLogService _expenseLogService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public ExpenseLogController(IExpenseLogService expenseLogService, IGarageContext context, IMapper mapper)
        {
            _expenseLogService = expenseLogService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ExpenseLogResponseDto>>> GetAll()
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<ExpenseLog> query = _context.ExpenseLogs.AsNoTracking();
            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x => x.BranchId == branchId);
            }

            var items = await query.ToListAsync();
            var dtos = _mapper.Map<IEnumerable<ExpenseLogResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ExpenseLogResponseDto>> GetById(long id)
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<ExpenseLog> query = _context.ExpenseLogs
                .AsNoTracking()
                .Where(x => x.Id == id);
            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x => x.BranchId == branchId);
            }

            var item = await query.FirstOrDefaultAsync();
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
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            var entity = _mapper.Map<ExpenseLog>(itemDto);
            if (scopedBranchId.HasValue)
            {
                entity.BranchId = scopedBranchId.Value;
            }
            var createdItem = await _expenseLogService.AddAsync(entity);
            var responseDto = _mapper.Map<ExpenseLogResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ExpenseLogResponseDto>> Update(long id, [FromBody] ExpenseLogUpdateDto itemDto)
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            var existingItem = await _expenseLogService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }
            if (scopedBranchId.HasValue && existingItem.BranchId != scopedBranchId.Value)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            if (scopedBranchId.HasValue)
            {
                existingItem.BranchId = scopedBranchId.Value;
            }
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
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            if (scopedBranchId.HasValue)
            {
                var inBranch = await _context.ExpenseLogs
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == id && x.BranchId == scopedBranchId.Value);
                if (!inBranch) return NotFound();
            }

            var result = await _expenseLogService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        private bool TryResolveAccountingBranchScope(out long? branchId, out ActionResult deniedResult)
        {
            branchId = null;
            deniedResult = null!;

            if (!(User.Identity?.IsAuthenticated ?? false))
            {
                deniedResult = Unauthorized();
                return false;
            }

            if (this.IsCurrentAccountingStaff())
            {
                branchId = this.GetCurrentBranchId();
                if (!branchId.HasValue)
                {
                    deniedResult = Forbid();
                    return false;
                }
            }

            return true;
        }
    }
}






