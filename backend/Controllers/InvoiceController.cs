using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/invoices")]
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
        public async Task<ActionResult<IEnumerable<InvoiceResponseDto>>> GetAll()
        {
            var items = await _invoiceService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<InvoiceResponseDto>>(items);
            return Ok(dtos);
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
                var lockError = await ValidateInvoiceMutationAsync(itemDto?.ServiceOrderId);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var entity = _mapper.Map<Invoice>(itemDto);
                var createdItem = await _invoiceService.AddAsync(entity);
                var responseDto = _mapper.Map<InvoiceResponseDto>(createdItem);
                return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> Update(long id, [FromBody] InvoiceUpdateDto itemDto)
        {
            try
            {
                var existingItem = await _invoiceService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var targetServiceOrderId = itemDto?.ServiceOrderId ?? existingItem.ServiceOrderId;
                var lockError = await ValidateInvoiceMutationAsync(targetServiceOrderId);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                _mapper.Map(itemDto, existingItem);
                var updatedItem = await _invoiceService.UpdateAsync(id, existingItem);

                if (updatedItem == null)
                {
                    return NotFound();
                }
                var responseDto = _mapper.Map<InvoiceResponseDto>(updatedItem);
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
                var existingItem = await _invoiceService.GetByIdAsync(id);
                if (existingItem == null)
                {
                    return NotFound();
                }

                var lockError = await ValidateInvoiceMutationAsync(existingItem.ServiceOrderId);
                if (!string.IsNullOrWhiteSpace(lockError))
                {
                    return BadRequest(new { message = lockError });
                }

                var result = await _invoiceService.DeleteAsync(id);
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






