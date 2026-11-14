using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Authorization;
using MyGarage.Application.Bays;
using MyGarage.Application.Bays.DTOs;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/bays")]
    [Route("api/v1/bays")]
    [ApiController]
    [Authorize]
    public class BaysController : ControllerBase
    {
        private readonly IBayApplicationService _bayAppService;
        private readonly IBayAuthorizationService _authService;

        public BaysController(
            IBayApplicationService bayAppService,
            IBayAuthorizationService authService)
        {
            _bayAppService = bayAppService;
            _authService = authService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BayResponseDto>>> GetAll([FromQuery] long? branchId = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var actor = this.ToUserActorContext();
            var items = await _bayAppService.GetBaysAsync(scopedBranchId, actor);
            var dtos = items.Select(MapToResponse).ToList();
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<BayResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var actor = this.ToUserActorContext();
            var item = await _bayAppService.GetBayByIdAsync(id, actor);
            if (item == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && item.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            return Ok(MapToResponse(item));
        }

        [HttpPost]
        public async Task<ActionResult<BayResponseDto>> Create([FromBody] BayCreateDto itemDto)
        {
            if (!this.TryResolveRequiredBranchId(itemDto?.BranchId > 0 ? itemDto.BranchId : null, out var targetBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var actor = this.ToUserActorContext();
            var created = await _bayAppService.CreateBayAsync(new BayCreateInput(
                targetBranchId,
                itemDto?.Code ?? string.Empty,
                itemDto?.Name ?? string.Empty,
                itemDto?.BayType,
                itemDto?.ServiceArea,
                itemDto?.MaxTonnage
            ), actor);

            var responseDto = MapToResponse(created);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<BayResponseDto>> Update(long id, [FromBody] BayUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return (ActionResult)denied!;
            }

            var actor = this.ToUserActorContext();
            var existing = await _bayAppService.GetBayByIdAsync(id, actor);
            if (existing == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && existing.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            var updated = await _bayAppService.UpdateBayAsync(id, new BayUpdateInput(
                itemDto?.Name,
                itemDto?.BayType,
                itemDto?.ServiceArea,
                itemDto?.MaxTonnage,
                itemDto?.OperationalStatus,
                itemDto?.IsActive
            ), actor);

            if (updated == null)
            {
                return NotFound();
            }

            return Ok(MapToResponse(updated));
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var actor = this.ToUserActorContext();
            var existing = await _bayAppService.GetBayByIdAsync(id, actor);
            if (existing == null)
            {
                return NotFound();
            }

            if (scopedBranchId.HasValue && existing.BranchId != scopedBranchId.Value)
            {
                return Forbid();
            }

            var result = await _bayAppService.DeleteBayAsync(id, actor);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }

        private static BayResponseDto MapToResponse(BayDetailDto b) => new()
        {
            Id = b.Id,
            BranchId = b.BranchId,
            Code = b.Code,
            Name = b.Name,
            BayType = b.BayType,
            ServiceArea = b.ServiceArea ?? string.Empty,
            MaxTonnage = b.MaxTonnage,
            OperationalStatus = b.OperationalStatus,
            CurrentOccupiedVehicleId = b.CurrentOccupiedVehicleId,
            IsActive = b.IsActive
        };
    }
}
