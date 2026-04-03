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
    [Route("api/purchase-orders")]
    [ApiController]
    public class PurchaseOrderController : ControllerBase
    {
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly IMapper _mapper;

        public PurchaseOrderController(IPurchaseOrderService purchaseOrderService, IMapper mapper)
        {
            _purchaseOrderService = purchaseOrderService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PurchaseOrderResponseDto>>> GetAll()
        {
            var items = await _purchaseOrderService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<PurchaseOrderResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PurchaseOrderResponseDto>> GetById(long id)
        {
            var item = await _purchaseOrderService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<PurchaseOrderResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<PurchaseOrderResponseDto>> Create([FromBody] PurchaseOrderCreateDto itemDto)
        {
            var entity = _mapper.Map<PurchaseOrder>(itemDto);
            var createdItem = await _purchaseOrderService.AddAsync(entity);
            var responseDto = _mapper.Map<PurchaseOrderResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PurchaseOrderResponseDto>> Update(long id, [FromBody] PurchaseOrderUpdateDto itemDto)
        {
            var existingItem = await _purchaseOrderService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _purchaseOrderService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<PurchaseOrderResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _purchaseOrderService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






