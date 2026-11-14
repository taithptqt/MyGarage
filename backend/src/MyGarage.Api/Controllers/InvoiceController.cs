using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/invoices")]
    [Route("api/v1/invoices")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IMapper _mapper;

        public InvoiceController(IInvoiceService invoiceService, IServiceOrderService serviceOrderService, IMapper mapper)
        {
            _invoiceService = invoiceService;
            _serviceOrderService = serviceOrderService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<InvoiceResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _invoiceService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _mapper.Map<IEnumerable<InvoiceResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> GetById(long id)
        {
            var item = await _invoiceService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<InvoiceResponseDto>(item);
            return Ok(dto);
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
                    return BadRequest(new { message = "Invoice must be created from a service order or retail order." });
                }

                var lockError = await ValidateInvoiceMutationAsync(itemDto?.ServiceOrderId);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var createdItem = await _invoiceService.EnsureForPaymentContextAsync(
                    itemDto?.ServiceOrderId,
                    itemDto?.RetailOrderId,
                    itemDto?.PayerType,
                    itemDto?.PayerId);

                if (createdItem == null)
                {
                return BadRequest(new { message = "Không thể tạo hóa đơn từ nguồn dữ liệu hiện tại." });
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
                var responseDto = _mapper.Map<InvoiceResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("backfill-completed-payments")]
        public async Task<ActionResult<InvoiceBackfillResponseDto>> BackfillCompletedPayments()
        {
            var result = await _invoiceService.BackfillCompletedPaymentsAsync();
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> Update(long id, [FromBody] InvoiceUpdateDto itemDto)
        {
            return StatusCode(405, new
            {
                    message = "Hóa đơn là chứng từ bất biến và không được phép chỉnh sửa trực tiếp."
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            return StatusCode(405, new
            {
                    message = "Hóa đơn là chứng từ bất biến và không được phép xóa trực tiếp."
            });
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






