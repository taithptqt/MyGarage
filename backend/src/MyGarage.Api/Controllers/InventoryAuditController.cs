using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/inventory-audits")]
    [Route("api/v1/inventory-audits")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class InventoryAuditController : ControllerBase
    {
        private readonly IInventoryAuditService _inventoryAuditService;
        private readonly IMapper _mapper;

        public InventoryAuditController(IInventoryAuditService inventoryAuditService, IMapper mapper)
        {
            _inventoryAuditService = inventoryAuditService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<InventoryAuditResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var envelope = await _inventoryAuditService.ToPagedEnvelopeAsync(
                page,
                pageSize,
                paged => _mapper.Map<IEnumerable<InventoryAuditResponseDto>>(paged));
            return Ok(envelope);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<InventoryAuditResponseDto>> GetById(long id)
        {
            var item = await _inventoryAuditService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<InventoryAuditResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<InventoryAuditResponseDto>> Create([FromBody] InventoryAuditCreateDto itemDto)
        {
            var entity = _mapper.Map<InventoryAudit>(itemDto);
            var createdItem = await _inventoryAuditService.AddAsync(entity);
            var responseDto = _mapper.Map<InventoryAuditResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<InventoryAuditResponseDto>> Update(long id, [FromBody] InventoryAuditUpdateDto itemDto)
        {
            var existingItem = await _inventoryAuditService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _inventoryAuditService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<InventoryAuditResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _inventoryAuditService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






