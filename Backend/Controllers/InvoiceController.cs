using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;
        private readonly IMapper _mapper;

        private static bool HasExactlyOneSource(long? serviceOrderId, long? retailOrderId)
        {
            var sourceCount = (serviceOrderId.HasValue ? 1 : 0) + (retailOrderId.HasValue ? 1 : 0);
            return sourceCount == 1;
        }

        private static bool IsManualPaymentSnapshotChange(InvoiceUpdateDto request, Invoice existing)
        {
            if (!string.IsNullOrWhiteSpace(request.PaymentStatus))
            {
                var requestedStatus = request.PaymentStatus.Trim();
                var currentStatus = (existing.PaymentStatus ?? string.Empty).Trim();
                if (!string.Equals(requestedStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (request.PaidAmount.HasValue)
            {
                var currentPaid = existing.PaidAmount ?? 0m;
                if (Math.Abs(request.PaidAmount.Value - currentPaid) > 0.005m)
                {
                    return true;
                }
            }

            return false;
        }

        public InvoiceController(IInvoiceService invoiceService, IMapper mapper)
        {
            _invoiceService = invoiceService;
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
            if (!HasExactlyOneSource(itemDto.ServiceOrderId, itemDto.RetailOrderId))
            {
                return BadRequest(new
                {
                    message = "Invoice must reference exactly one source: ServiceOrderId or RetailOrderId."
                });
            }

            var entity = _mapper.Map<Invoice>(itemDto);
            var createdItem = await _invoiceService.AddAsync(entity);
            var responseDto = _mapper.Map<InvoiceResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceResponseDto>> Update(long id, [FromBody] InvoiceUpdateDto itemDto)
        {
            var existingItem = await _invoiceService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            if (!HasExactlyOneSource(itemDto.ServiceOrderId, itemDto.RetailOrderId))
            {
                return BadRequest(new
                {
                    message = "Invoice must reference exactly one source: ServiceOrderId or RetailOrderId."
                });
            }

            if (IsManualPaymentSnapshotChange(itemDto, existingItem))
            {
                return BadRequest(new
                {
                    message = "Cannot manually update PaidAmount/PaymentStatus. These fields are synchronized from Payments."
                });
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _invoiceService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}


