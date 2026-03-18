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
    public class StockTransactionController : ControllerBase
    {
        private readonly IStockTransactionService _stockTransactionService;
        private readonly IMapper _mapper;

        public StockTransactionController(IStockTransactionService stockTransactionService, IMapper mapper)
        {
            _stockTransactionService = stockTransactionService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<StockTransactionResponseDto>>> GetAll()
        {
            var items = await _stockTransactionService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<StockTransactionResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StockTransactionResponseDto>> GetById(long id)
        {
            var item = await _stockTransactionService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<StockTransactionResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StockTransactionResponseDto>> Create([FromBody] StockTransactionCreateDto itemDto)
        {
            var entity = _mapper.Map<StockTransaction>(itemDto);
            var createdItem = await _stockTransactionService.AddAsync(entity);
            var responseDto = _mapper.Map<StockTransactionResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StockTransactionResponseDto>> Update(long id, [FromBody] StockTransactionUpdateDto itemDto)
        {
            var existingItem = await _stockTransactionService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _stockTransactionService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<StockTransactionResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _stockTransactionService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}





