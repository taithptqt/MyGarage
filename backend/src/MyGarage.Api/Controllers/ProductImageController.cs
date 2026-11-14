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

namespace MyGarage.Controllers
{
    [Route("api/product-images")]
    [Route("api/v1/product-images")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ProductImageController : ControllerBase
    {
        private readonly IProductImageService _productImageService;
        private readonly IMapper _mapper;

        public ProductImageController(IProductImageService productImageService, IMapper mapper)
        {
            _productImageService = productImageService;
            _mapper = mapper;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductImageResponseDto>>> GetAll([FromQuery] long? productId = null)
        {
            var items = await _productImageService.GetAllAsync();
            if (productId.HasValue)
            {
                items = items.Where(item => item.ProductId == productId.Value).ToList();
            }

            var dtos = _mapper.Map<IEnumerable<ProductImageResponseDto>>(items);
            return Ok(dtos);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductImageResponseDto>> GetById(long id)
        {
            var item = await _productImageService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<ProductImageResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ProductImageResponseDto>> Create([FromBody] ProductImageCreateDto itemDto)
        {
            var entity = _mapper.Map<ProductImage>(itemDto);
            var createdItem = await _productImageService.AddAsync(entity);
            var responseDto = _mapper.Map<ProductImageResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ProductImageResponseDto>> Update(long id, [FromBody] ProductImageUpdateDto itemDto)
        {
            var existingItem = await _productImageService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _productImageService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<ProductImageResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _productImageService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






