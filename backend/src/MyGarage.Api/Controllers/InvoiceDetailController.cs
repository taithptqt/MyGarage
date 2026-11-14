using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using Microsoft.EntityFrameworkCore;

namespace MyGarage.Controllers
{
    [Route("api/invoice-details")]
    [Route("api/v1/invoice-details")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class InvoiceDetailController : ControllerBase
    {
        private readonly IInvoiceDetailService _invoiceDetailService;
        private readonly IServiceOrderService _serviceOrderService;
        private readonly IGarageContext _context;
        private readonly IMapper _mapper;

        public InvoiceDetailController(
            IInvoiceDetailService invoiceDetailService,
            IServiceOrderService serviceOrderService,
            IGarageContext context,
            IMapper mapper)
        {
            _invoiceDetailService = invoiceDetailService;
            _serviceOrderService = serviceOrderService;
            _context = context;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<InvoiceDetailResponseDto>>> GetAll()
        {
            var items = await _invoiceDetailService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<InvoiceDetailResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceDetailResponseDto>> GetById(long id)
        {
            var item = await _invoiceDetailService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<InvoiceDetailResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceDetailResponseDto>> Create([FromBody] InvoiceDetailCreateDto itemDto)
        {
            try
            {
                var lockError = await ValidateInvoiceDetailMutationAsync(itemDto?.InvoiceId, null);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var entity = _mapper.Map<InvoiceDetail>(itemDto);
                var createdItem = await _invoiceDetailService.AddAsync(entity);
                var responseDto = _mapper.Map<InvoiceDetailResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceDetailResponseDto>> Update(long id, [FromBody] InvoiceDetailUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _invoiceDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var invoiceId = itemDto?.InvoiceId > 0 ? itemDto.InvoiceId : existingItem.InvoiceId;
                var lockError = await ValidateInvoiceDetailMutationAsync(invoiceId, existingItem);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _invoiceDetailService.UpdateAsync(id, existingItem);
                if (updatedItem == null)
                {
                    return NotFound();
                }

                var responseDto = _mapper.Map<InvoiceDetailResponseDto>(updatedItem);
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            try
            {
                var existingItem = await _invoiceDetailService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var lockError = await ValidateInvoiceDetailMutationAsync(existingItem.InvoiceId, existingItem);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var result = await _invoiceDetailService.DeleteAsync(id);
                if (!result)
                {
                    return NotFound();
                }
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task<string?> ValidateInvoiceDetailMutationAsync(long? invoiceId, InvoiceDetail? existingItem)
        {
            var targetInvoiceId = invoiceId.GetValueOrDefault();
            if (targetInvoiceId <= 0 && existingItem != null)
            {
                targetInvoiceId = existingItem.InvoiceId;
            }

            if (targetInvoiceId <= 0)
            {
                return null;
            }

            var serviceOrderId = await _context.Invoices
                .AsNoTracking()
                .Where(invoice => invoice.Id == targetInvoiceId)
                .Select(invoice => invoice.ServiceOrderId)
                .FirstOrDefaultAsync();
            if (!serviceOrderId.HasValue || serviceOrderId.Value <= 0)
            {
                return null;
            }

            var capability = await _serviceOrderService.GetEditCapabilityAsync(serviceOrderId.Value);
            if (capability == null)
            {
                return "Không tìm thấy service order để xử lý chi tiết hóa đơn.";
            }

            if (capability.IsClosedLocked)
            {
                return "Không thể chỉnh sửa chi tiết hóa đơn vì service order đã đóng.";
            }

            if (capability.IsBillingLocked)
            {
                return "Không thể chỉnh sửa chi tiết hóa đơn vì lệnh đã chốt tài chính.";
            }

            return null;
        }
    }
}






