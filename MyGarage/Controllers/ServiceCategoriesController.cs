using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Controllers
{
    [Route("api/service-categories")]
    [ApiController]
    public class ServiceCategoriesController : ControllerBase
    {
        private readonly IServiceCategoryService _serviceCategoryService;
        private readonly IMapper _mapper;

        public ServiceCategoriesController(IServiceCategoryService serviceCategoryService, IMapper mapper)
        {
            _serviceCategoryService = serviceCategoryService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ServiceCategoryResponseDto>>> GetAll()
        {
            var items = await _serviceCategoryService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<ServiceCategoryResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<ServiceCategoryResponseDto>> GetById(long id)
        {
            var item = await _serviceCategoryService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            var dto = _mapper.Map<ServiceCategoryResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<ServiceCategoryResponseDto>> Create([FromBody] ServiceCategoryCreateDto itemDto)
        {
            var entity = _mapper.Map<ServiceCategory>(itemDto);
            var createdItem = await _serviceCategoryService.AddAsync(entity);
            var responseDto = _mapper.Map<ServiceCategoryResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<ServiceCategoryResponseDto>> Update(long id, [FromBody] ServiceCategoryUpdateDto itemDto)
        {
            var existingItem = await _serviceCategoryService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _serviceCategoryService.UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound();
            }

            var responseDto = _mapper.Map<ServiceCategoryResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _serviceCategoryService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
