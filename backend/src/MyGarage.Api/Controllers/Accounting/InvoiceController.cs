using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Invoices;
using MyGarage.Application.Authorization;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/v1/invoices")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IInvoiceRepository _invoiceRepository;
        private readonly IInvoiceBranchLookupStore _branchLookup;
        private readonly IInvoiceAuthorizationService _authorization;

        public InvoiceController(IInvoiceService invoiceService, IServiceOrderService serviceOrderService,
            IInvoiceRepository invoiceRepository, IInvoiceBranchLookupStore branchLookup, IInvoiceAuthorizationService authorization)
        {
            _invoiceService = invoiceService;
            _serviceOrderService = serviceOrderService;
            _invoiceRepository = invoiceRepository;
            _branchLookup = branchLookup;
            _authorization = authorization;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<InvoiceResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (items, total) = await _invoiceRepository.GetScopedPageAsync(branchId, normalizedPage, normalizedSize, HttpContext.RequestAborted);
            var envelope = PagedEnvelope<InvoiceResponseDto>.Create(_invoiceService.MapToDtos(items), total, normalizedPage, normalizedSize);
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> GetById(long id)
        {
            if (!this.TryResolveBranchScope(null, out var branchId, out var denied)) return (ActionResult)denied!;
            var item = await _invoiceRepository.GetScopedAsync(id, branchId, HttpContext.RequestAborted);
            if (item == null)
            {
                return NotFound();
            }
            return Ok(_invoiceService.MapToDto(item));
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceResponseDto>> Create([FromBody] InvoiceCreateDto itemDto)
        {
            try
            {
                var actorUserId = this.GetCurrentUserId();
                if (actorUserId == null)
                {
                    return Unauthorized();
                }

                if ((itemDto?.ServiceOrderId ?? 0) <= 0 && (itemDto?.RetailOrderId ?? 0) <= 0)
                {
                    return this.BadRequestProblem("Invoice must be created from a service order or retail order.");
                }

                if (!this.TryResolveBranchScope(null, out var scopedBranchId, out var denied)) return (ActionResult)denied!;
                if ((itemDto?.ServiceOrderId ?? 0) > 0 && (itemDto?.RetailOrderId ?? 0) > 0)
                    return this.BadRequestProblem("Invoice must have exactly one source order.");
                var sourceBranch = (itemDto?.ServiceOrderId ?? 0) > 0
                    ? await _branchLookup.GetBranchIdForServiceOrderAsync(itemDto!.ServiceOrderId!.Value, HttpContext.RequestAborted)
                    : await _branchLookup.GetBranchIdForRetailOrderAsync(itemDto!.RetailOrderId!.Value, HttpContext.RequestAborted);
                if (!sourceBranch.HasValue) return NotFound();
                if (scopedBranchId.HasValue && sourceBranch.Value != scopedBranchId.Value) return Forbid();
                if (!await _authorization.CanCreateInvoiceAsync(sourceBranch, this.ToUserActorContext(), HttpContext.RequestAborted)) return Forbid();

                var lockError = await ValidateInvoiceMutationAsync(itemDto?.ServiceOrderId);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return this.BadRequestProblem(lockError);
                }

                var outcome = await _invoiceService.EnsureForPaymentContextWithOutcomeAsync(
                    itemDto?.ServiceOrderId,
                    itemDto?.RetailOrderId,
                    itemDto?.PayerType,
                    itemDto?.PayerId);
                var createdItem = outcome.Invoice;

                if (createdItem == null)
                {
                return this.BadRequestProblem("Không thể tạo hóa đơn từ nguồn dữ liệu hiện tại.");
                }

                createdItem.Phone = itemDto?.Phone ?? createdItem.Phone;
                createdItem.Address = itemDto?.Address ?? createdItem.Address;
                createdItem.InvoiceUrl = itemDto?.InvoiceUrl ?? createdItem.InvoiceUrl;
                createdItem.CompanyName = itemDto?.CompanyName ?? createdItem.CompanyName;
                createdItem.CompanyAddress = itemDto?.CompanyAddress ?? createdItem.CompanyAddress;
                createdItem.ReceiveEmail = itemDto?.ReceiveEmail ?? createdItem.ReceiveEmail;
                createdItem.TaxCode = itemDto?.TaxCode ?? createdItem.TaxCode;
                createdItem.CreditNote = itemDto?.CreditNote ?? createdItem.CreditNote;
                createdItem.IssuedAt ??= itemDto?.IssuedAt;
                createdItem.CreatedBy ??= actorUserId.Value;
                createdItem.UpdatedBy = actorUserId.Value;
                createdItem.UpdatedAt = VietnamTime.Now;

                createdItem = await _invoiceService.UpdateAsync(createdItem.Id, createdItem) ?? createdItem;
                var responseDto = _invoiceService.MapToDto(createdItem);
                return outcome.Created
                    ? CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto)
                    : Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return this.BadRequestProblem(ex.Message);
            }
        }

        [HttpPost("completed-payment-backfills")]
        public async Task<ActionResult<InvoiceBackfillResponseDto>> BackfillCompletedPayments()
        {
            if (!this.IsAdminStaff()) return Forbid();
            var result = await _invoiceService.BackfillCompletedPaymentsAsync();
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> Update(long id, [FromBody] InvoiceUpdateDto itemDto)
        {
            return this.ProblemMessage(405, "Hóa đơn là chứng từ bất biến và không được phép chỉnh sửa trực tiếp.");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            return this.ProblemMessage(405, "Hóa đơn là chứng từ bất biến và không được phép xóa trực tiếp.");
        }

        private async Task<string?> ValidateInvoiceMutationAsync(long? serviceOrderId)
        {
            if (!serviceOrderId.HasValue || serviceOrderId.Value <= 0)
            {
                return null;
            }

            var capability = await _serviceOrderService.GetEditCapabilityAsync(serviceOrderId.Value);
            if (capability == null)
            {
                return "Không tìm thấy service order để xử lý hóa đơn.";
            }

            if (capability.IsClosedLocked)
            {
                return "Không thể chỉnh sửa hóa đơn vì service order đã đóng.";
            }

            if (capability.IsBillingLocked)
            {
                return "Không thể chỉnh sửa hóa đơn vì lệnh đã chốt tài chính.";
            }

            return null;
        }
    }
}






