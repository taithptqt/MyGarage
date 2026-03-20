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
    [Route("api/[controller]")]
    [ApiController]
    public class InvoiceDetailController : ControllerBase
    {
        private readonly IInvoiceDetailService _invoiceDetailService;
        private readonly IMapper _mapper;

        public InvoiceDetailController(IInvoiceDetailService invoiceDetailService, IMapper mapper)
        {
            _invoiceDetailService = invoiceDetailService;
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
            var entity = _mapper.Map<InvoiceDetail>(itemDto);
            var createdItem = await _invoiceDetailService.AddAsync(entity);
            var responseDto = _mapper.Map<InvoiceDetailResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InvoiceDetailResponseDto>> Update(long id, [FromBody] InvoiceDetailUpdateDto itemDto)
        {
            var existingItem = await _invoiceDetailService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _invoiceDetailService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





