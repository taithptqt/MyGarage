using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Enums;

namespace MyGarage.Controllers
{
    [Authorize(Roles = "Staff")]
    [Route("api/invoices")]
    [ApiController]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public InvoiceController(
            IInvoiceService invoiceService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _invoiceService = invoiceService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvoiceResponseDto>>> GetAll()
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<Invoice> query = _context.Invoices
                .AsNoTracking();

            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x =>
                    (x.ServiceOrderId.HasValue && _context.ServiceOrders
                        .Any(so => so.Id == x.ServiceOrderId.Value
                            && (so.ExecuteBranchId == branchId || so.OriginBranchId == branchId)))
                    || (x.RetailOrderId.HasValue && _context.RetailOrders
                        .Any(ro => ro.Id == x.RetailOrderId.Value && ro.BranchId == branchId))
                    || x.Payments.Any(p =>
                        p.Status != null
                        && p.Status.ToUpper() == nameof(PaymentStatusCode.COMPLETED)
                        && p.Cashier != null
                        && p.Cashier.BranchId == branchId));
            }

            var items = await query.ToListAsync();
            var dtos = _mapper.Map<IEnumerable<InvoiceResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> GetById(long id)
        {
            if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

            IQueryable<Invoice> query = _context.Invoices
                .AsNoTracking()
                .Where(x => x.Id == id);

            if (scopedBranchId.HasValue)
            {
                var branchId = scopedBranchId.Value;
                query = query.Where(x =>
                    (x.ServiceOrderId.HasValue && _context.ServiceOrders
                        .Any(so => so.Id == x.ServiceOrderId.Value
                            && (so.ExecuteBranchId == branchId || so.OriginBranchId == branchId)))
                    || (x.RetailOrderId.HasValue && _context.RetailOrders
                        .Any(ro => ro.Id == x.RetailOrderId.Value && ro.BranchId == branchId))
                    || x.Payments.Any(p =>
                        p.Status != null
                        && p.Status.ToUpper() == nameof(PaymentStatusCode.COMPLETED)
                        && p.Cashier != null
                        && p.Cashier.BranchId == branchId));
            }

            var item = await query.FirstOrDefaultAsync();
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
                if (!TryResolveAccountingBranchScope(out var scopedBranchId, out var denied)) return denied;

                var actorUserId = this.GetCurrentUserId();
                if (actorUserId == null)
                {
                    return Unauthorized();
                }

                if ((itemDto?.ServiceOrderId ?? 0) <= 0 && (itemDto?.RetailOrderId ?? 0) <= 0)
                {
                    return BadRequest(new { message = "Invoice must be created from a service order or retail order." });
                }

                if (scopedBranchId.HasValue)
                {
                    var inBranch = await IsInvoiceSourceInBranchAsync(itemDto?.ServiceOrderId, itemDto?.RetailOrderId, scopedBranchId.Value);
                    if (!inBranch)
                    {
                        return NotFound();
                    }
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
                    return BadRequest(new { message = "Khong the tao hoa don tu nguon du lieu hien tai." });
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

                if (scopedBranchId.HasValue)
                {
                    var inBranch = await IsInvoiceEntityInBranchAsync(createdItem, scopedBranchId.Value);
                    if (!inBranch)
                    {
                        return NotFound();
                    }
                }

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
                message = "Hoa don la chung tu bat bien va khong duoc phep chinh sua truc tiep."
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            return StatusCode(405, new
            {
                message = "Hoa don la chung tu bat bien va khong duoc phep xoa truc tiep."
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

        private async Task<bool> IsInvoiceSourceInBranchAsync(long? serviceOrderId, long? retailOrderId, long branchId)
        {
            if (serviceOrderId.HasValue && serviceOrderId.Value > 0)
            {
                var serviceOrderInBranch = await _context.ServiceOrders
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == serviceOrderId.Value && (x.ExecuteBranchId == branchId || x.OriginBranchId == branchId));
                if (!serviceOrderInBranch) return false;
            }

            if (retailOrderId.HasValue && retailOrderId.Value > 0)
            {
                var retailOrderInBranch = await _context.RetailOrders
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == retailOrderId.Value && x.BranchId == branchId);
                if (!retailOrderInBranch) return false;
            }

            return true;
        }

        private async Task<bool> IsInvoiceEntityInBranchAsync(Invoice invoice, long branchId)
        {
            return await IsInvoiceSourceInBranchAsync(invoice.ServiceOrderId, invoice.RetailOrderId, branchId);
        }
    }
}





