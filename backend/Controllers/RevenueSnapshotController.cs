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
    [Route("api/revenue-snapshots")]
    [ApiController]
    public class RevenueSnapshotController : ControllerBase
    {
        private readonly IRevenueSnapshotService _revenueSnapshotService;
        private readonly IGarageContext _db;
        private readonly IMapper _mapper;

        public RevenueSnapshotController(IRevenueSnapshotService revenueSnapshotService, IGarageContext db, IMapper mapper)
        {
            _revenueSnapshotService = revenueSnapshotService;
            _db = db;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RevenueSnapshotResponseDto>>> GetAll()
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<RevenueSnapshot> query = _db.RevenueSnapshots.AsNoTracking();
            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x => x.BranchId == branchId);
            }

            var items = await query.ToListAsync();
            var dtos = _mapper.Map<IEnumerable<RevenueSnapshotResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> GetById(long id)
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<RevenueSnapshot> query = _db.RevenueSnapshots
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
            var dto = _mapper.Map<RevenueSnapshotResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> Create([FromBody] RevenueSnapshotCreateDto itemDto)
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            var entity = _mapper.Map<RevenueSnapshot>(itemDto);
            if (scopedBranchId.HasValue)
            {
                entity.BranchId = scopedBranchId.Value;
            }
            var createdItem = await _revenueSnapshotService.AddAsync(entity);
            var responseDto = _mapper.Map<RevenueSnapshotResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<RevenueSnapshotResponseDto>> Update(long id, [FromBody] RevenueSnapshotUpdateDto itemDto)
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            var existingItem = await _db.RevenueSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
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
            var updatedItem = await _revenueSnapshotService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<RevenueSnapshotResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            if (scopedBranchId.HasValue)
            {
                var inBranch = await _db.RevenueSnapshots
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == id && x.BranchId == scopedBranchId.Value);
                if (!inBranch) return NotFound();
            }

            var result = await _revenueSnapshotService.DeleteAsync(id);
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







