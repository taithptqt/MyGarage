using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Authorization;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/purchase-order-items")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class PurchaseOrderItemController : ControllerBase
    {
        private readonly IPurchaseOrderItemService _purchaseOrderItemService;
        private readonly IPurchaseOrderItemQueryService _purchaseOrderItemQueryService;
        private readonly IPurchaseOrderAuthorizationService _authorization;

        public PurchaseOrderItemController(
            IPurchaseOrderItemService purchaseOrderItemService,
            IPurchaseOrderItemQueryService purchaseOrderItemQueryService,
            IPurchaseOrderAuthorizationService authorization)
        {
            _purchaseOrderItemService = purchaseOrderItemService;
            _purchaseOrderItemQueryService = purchaseOrderItemQueryService;
            _authorization = authorization;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return denied!;
            var items = await _purchaseOrderItemQueryService.GetAllByBranchAsync(branchId);
            var dtos = _purchaseOrderItemService.MapToDtos(items);
            return Ok(dtos);
        }

        [HttpGet("page")]
        public async Task<ActionResult<PagedEnvelope<PurchaseOrderItemResponseDto>>> GetPage(
            [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (items, total) = await _purchaseOrderItemQueryService.GetPageByBranchAsync(
                branchId, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            return Ok(PagedEnvelope<PurchaseOrderItemResponseDto>.Create(
                _purchaseOrderItemService.MapToDtos(items), total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PurchaseOrderItemResponseDto>> GetById(long id)
        {
            var item = await _purchaseOrderItemService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            if (!await _authorization.CanViewPurchaseOrderAsync(item.PurchaseOrderId, this.ToUserActorContext())) return Forbid();
            var dto = _purchaseOrderItemService.MapToDto(item);
            return Ok(dto);
        }

        [HttpPost]
        public ActionResult<PurchaseOrderItemResponseDto> Create([FromBody] PurchaseOrderItemCreateDto itemDto)
        {
            return this.ConflictProblem("Không hỗ trợ thay đổi dòng đơn mua qua API này.");
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PurchaseOrderItemResponseDto>> Update(long id, [FromBody] PurchaseOrderItemUpdateDto itemDto)
        {
            var existingItem = await _purchaseOrderItemService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!await _authorization.CanUpdatePurchaseOrderAsync(existingItem.PurchaseOrderId, this.ToUserActorContext())) return Forbid();
            return this.ConflictProblem("Không hỗ trợ thay đổi dòng đơn mua qua API này.");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var item = await _purchaseOrderItemService.GetByIdAsync(id);
            if (item == null) return NotFound();
            if (!await _authorization.CanUpdatePurchaseOrderAsync(item.PurchaseOrderId, this.ToUserActorContext())) return Forbid();
            return this.ConflictProblem("Không hỗ trợ thay đổi dòng đơn mua qua API này.");
        }
    }
}






