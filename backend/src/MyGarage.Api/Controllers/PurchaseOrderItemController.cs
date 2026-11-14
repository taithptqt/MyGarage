using System.Collections.Generic;
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
    [Route("api/purchase-order-items")]
    [Route("api/v1/purchase-order-items")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class PurchaseOrderItemController : ControllerBase
    {
        private readonly IPurchaseOrderItemService _purchaseOrderItemService;
        private readonly IMapper _mapper;

        public PurchaseOrderItemController(IPurchaseOrderItemService purchaseOrderItemService, IMapper mapper)
        {
            _purchaseOrderItemService = purchaseOrderItemService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PurchaseOrderItemResponseDto>>> GetAll()
        {
            var items = await _purchaseOrderItemService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<PurchaseOrderItemResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PurchaseOrderItemResponseDto>> GetById(long id)
        {
            var item = await _purchaseOrderItemService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<PurchaseOrderItemResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<PurchaseOrderItemResponseDto>> Create([FromBody] PurchaseOrderItemCreateDto itemDto)
        {
            var entity = _mapper.Map<PurchaseOrderItem>(itemDto);
            var createdItem = await _purchaseOrderItemService.AddAsync(entity);
            var responseDto = _mapper.Map<PurchaseOrderItemResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<PurchaseOrderItemResponseDto>> Update(long id, [FromBody] PurchaseOrderItemUpdateDto itemDto)
        {
            var existingItem = await _purchaseOrderItemService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _purchaseOrderItemService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<PurchaseOrderItemResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _purchaseOrderItemService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






