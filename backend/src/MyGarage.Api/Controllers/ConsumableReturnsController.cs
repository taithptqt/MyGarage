using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests.Execution;
using MyGarage.DTOs.Responses.Execution;
using MyGarage.Exceptions;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [ApiController]
    [Route("api/consumable-returns")]
    [Route("api/v1/consumable-returns")]
    [Authorize]
    public sealed class ConsumableReturnsController : ControllerBase
    {
        private readonly IConsumableReturnService _consumableReturnService;
        private readonly IInventoryAdminService _inventoryAdminService;

        public ConsumableReturnsController(
            IConsumableReturnService consumableReturnService,
            IInventoryAdminService inventoryAdminService)
        {
            _consumableReturnService = consumableReturnService;
            _inventoryAdminService = inventoryAdminService;
        }

        [HttpPost]
        public async Task<ActionResult<ConsumableReturnBundleDto>> Create([FromBody] ConsumableReturnCreateRequestDto request)
        {
            var actorId = ResolveActorStaffId();
            if (!actorId.HasValue || actorId.Value <= 0)
            {
                return Unauthorized(new { message = "Không xác định được nhân sự thao tác." });
            }

            try
            {
                var data = await _consumableReturnService.CreateBundleAsync(request, actorId.Value);
                await _inventoryAdminService.RebuildStockAlertsAsync(actorId);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (BadRequestException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private long? ResolveActorStaffId()
        {
            return this.GetCurrentUserId();
        }
    }
}
