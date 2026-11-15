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
    [Route("api/v1/consumable-returns")]
    [Authorize]
    public sealed class ConsumableReturnsController : ControllerBase
    {
        private readonly IConsumableReturnService _consumableReturnService;
        private readonly IStockAlertSyncService _stockAlertSyncService;

        public ConsumableReturnsController(
            IConsumableReturnService consumableReturnService,
            IStockAlertSyncService stockAlertSyncService)
        {
            _consumableReturnService = consumableReturnService;
            _stockAlertSyncService = stockAlertSyncService;
        }

        [HttpPost]
        public async Task<ActionResult<ConsumableReturnBundleDto>> Create([FromBody] ConsumableReturnCreateRequestDto request)
        {
            var actorId = ResolveActorStaffId();
            if (!actorId.HasValue || actorId.Value <= 0)
            {
                return this.UnauthorizedProblem("Không xác định được nhân sự thao tác.");
            }

            try
            {
                var data = await _consumableReturnService.CreateBundleAsync(request, actorId.Value);
                await _stockAlertSyncService.RebuildStockAlertsAsync(actorId);
                return Ok(data);
            }
            catch (NotFoundException ex)
            {
                return this.NotFoundProblem(ex.Message);
            }
            catch (BadRequestException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        private long? ResolveActorStaffId()
        {
            return this.GetCurrentUserId();
        }
    }
}
