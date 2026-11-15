using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.Common;

namespace MyGarage.Controllers
{
    [ApiController]
    [Route("api/v1/workshop")]
    [Authorize]
    public sealed class WorkshopDispatchController : ControllerBase
    {
        private readonly IWorkshopDispatchService _workshopDispatchService;

        public WorkshopDispatchController(IWorkshopDispatchService workshopDispatchService)
        {
            _workshopDispatchService = workshopDispatchService;
        }

        [HttpGet("dispatch-board")]
        public async Task<IActionResult> GetDispatchBoard([FromQuery] long? branchId, [FromQuery] DateTime? date)
        {
            var targetBranchId = branchId ?? this.GetCurrentBranchId();
            if (targetBranchId <= 0)
            {
                return this.BadRequestProblem("Thiếu branchId hợp lệ.");
            }

            var targetDate = date ?? VietnamTime.Now;
            var resolvedBranchId = targetBranchId.GetValueOrDefault();
            var board = await _workshopDispatchService.GetBoardAsync(resolvedBranchId, targetDate);
            return Ok(board);
        }
    }
}
