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
    public class CommissionTransactionController : ControllerBase
    {
        private readonly ICommissionTransactionService _commissionTransactionService;
        private readonly IMapper _mapper;

        public CommissionTransactionController(ICommissionTransactionService commissionTransactionService, IMapper mapper)
        {
            _commissionTransactionService = commissionTransactionService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommissionTransactionResponseDto>>> GetAll()
        {
            var items = await _commissionTransactionService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CommissionTransactionResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommissionTransactionResponseDto>> GetById(long id)
        {
            var item = await _commissionTransactionService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CommissionTransactionResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CommissionTransactionResponseDto>> Create([FromBody] CommissionTransactionCreateDto itemDto)
        {
            var entity = _mapper.Map<CommissionTransaction>(itemDto);
            var createdItem = await _commissionTransactionService.AddAsync(entity);
            var responseDto = _mapper.Map<CommissionTransactionResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CommissionTransactionResponseDto>> Update(long id, [FromBody] CommissionTransactionUpdateDto itemDto)
        {
            var existingItem = await _commissionTransactionService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _commissionTransactionService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CommissionTransactionResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _commissionTransactionService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





