using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Purchasing;
using MyGarage.Application.Purchasing.DTOs;
using MyGarage.DTOs.Requests;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/purchase-orders")]
    [ApiController]
    [Authorize]
    public class PurchaseOrderController : ControllerBase
    {
        public sealed record PurchaseOrderApproveRequestDto(long? ApprovedBy, string? Note);

        private readonly IPurchaseOrderCommandFacade _poCommandFacade;
        private readonly IPurchaseOrderApplicationService _poAppService;

        public PurchaseOrderController(
            IPurchaseOrderCommandFacade poCommandFacade,
            IPurchaseOrderApplicationService poAppService)
        {
            _poCommandFacade = poCommandFacade;
            _poAppService = poAppService;
        }

        private IActionResult MapResult<T>(PurchaseOrderCommandResult<T> result) => result.StatusCode switch
        {
            200 => Ok(result.Data),
            400 => BadRequest(result.ErrorPayload),
            403 => Forbid(),
            404 => result.ErrorPayload == null ? NotFound() : NotFound(result.ErrorPayload),
            _ => StatusCode(result.StatusCode, result.ErrorPayload)
        };

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] long? branchId = null,
            [FromQuery] string? status = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] bool envelope = false,
            [FromQuery] string? statusGroup = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _poCommandFacade.GetAllAsync(
                scopedBranchId, status, page, pageSize, envelope, statusGroup, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _poCommandFacade.GetByIdAsync(id, scopedBranchId, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PurchaseOrderCreateDto itemDto)
        {
            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "STAFF", "OWNER", "DIRECTOR", "QUAN_DOC", "STOREKEEPER", "STORE_KEEPER"))
            {
                return Forbid();
            }

            if (itemDto == null)
            {
                return this.BadRequestProblem("Dữ liệu đơn mua hàng không hợp lệ.");
            }

            if (!this.TryResolveRequiredBranchId(
                    itemDto.BranchId > 0 ? itemDto.BranchId : null,
                    out var targetBranchId,
                    out var denied))
            {
                return denied!;
            }

            var result = await _poCommandFacade.CreateAsync(itemDto, targetBranchId, this.ToUserActorContext());
            if (result.StatusCode == 201 && result.Data != null)
            {
                return CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result.Data.Payload);
            }

            return MapResult(result);
        }

        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] PurchaseOrderUpdateDto itemDto)
        {
            if (!this.TryResolveBranchScope(itemDto?.BranchId > 0 ? itemDto.BranchId : null, out _, out var denied))
            {
                return denied!;
            }

            var result = await _poCommandFacade.UpdateAsync(id, itemDto, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpPost("{id:long}/approvals")]
        public async Task<IActionResult> Approve(long id, [FromBody] PurchaseOrderApproveRequestDto? payload = null)
        {
            if (!this.TryResolveBranchScope(null, out _, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "SERVICE_MANAGER", "OWNER", "DIRECTOR", "QUAN_DOC"))
            {
                return Forbid();
            }

            var result = await _poCommandFacade.ApproveAsync(id, payload?.ApprovedBy, payload?.Note, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpPost("{id:long}/goods-receipts")]
        public async Task<IActionResult> Receive(long id, [FromBody] PurchaseOrderReceiveRequestDto? request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return this.BadRequestProblem("Thiếu payload nhận hàng.");
            }

            if (!this.TryResolveBranchScope(null, out _, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "WAREHOUSE", "STOREKEEPER", "STORE_KEEPER", "INVENTORY", "THU_KHO"))
            {
                return Forbid();
            }

            var result = await _poCommandFacade.ReceiveAsync(id, request, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpGet("{id:long}/goods-receipts")]
        [HttpGet("{id}/receive-history")]
        public async Task<IActionResult> GetReceiveHistory(long id)
        {
            if (!this.TryResolveBranchScope(null, out _, out var denied))
            {
                return denied!;
            }

            var result = await _poCommandFacade.GetReceiveHistoryAsync(id, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpGet("{id}/fulfillment")]
        public async Task<IActionResult> GetFulfillment(long id)
        {
            if (!this.TryResolveBranchScope(null, out _, out var denied))
            {
                return denied!;
            }

            var result = await _poCommandFacade.GetFulfillmentAsync(id, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpGet("fulfillment")]
        public async Task<IActionResult> GetFulfillmentDashboard(
            [FromQuery] long? branchId = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            var result = await _poCommandFacade.GetFulfillmentDashboardAsync(
                scopedBranchId, fromDate, toDate, page, pageSize, this.ToUserActorContext());
            return MapResult(result);
        }

        [HttpGet("grn-kpis")]
        public async Task<IActionResult> GetGrnKpis(
            [FromQuery] long? branchId = null,
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            try
            {
                var kpis = await _poAppService.GetGrnKpisAsync(new GrnKpiFilterDto(
                    scopedBranchId,
                    fromDate,
                    toDate
                ), this.ToUserActorContext());

                return Ok(kpis);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost("grn-snapshots/rebuilds")]
        public async Task<IActionResult> RebuildGrnSnapshot(
            [FromQuery] DateOnly? snapshotDate = null,
            [FromQuery] long? branchId = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            try
            {
                var result = await _poAppService.RebuildGrnSnapshotAsync(snapshotDate, scopedBranchId, this.ToUserActorContext());
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpGet("grn-snapshots/trend")]
        public async Task<IActionResult> GetGrnSnapshotTrend(
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null,
            [FromQuery] long? branchId = null)
        {
            if (!this.TryResolveBranchScope(branchId, out var scopedBranchId, out var denied))
            {
                return denied!;
            }

            try
            {
                var result = await _poAppService.GetGrnSnapshotTrendAsync(fromDate, toDate, scopedBranchId, this.ToUserActorContext());
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (!this.TryResolveBranchScope(null, out _, out var denied))
            {
                return denied!;
            }

            if (!this.IsStaffWithAnyKeyword("ADMIN", "MANAGER", "DIRECTOR", "OWNER"))
            {
                return Forbid();
            }

            try
            {
                var result = await _poAppService.DeletePurchaseOrderAsync(id, this.ToUserActorContext());
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }
    }
}
