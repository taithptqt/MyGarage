using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/ai-model-configs")]
    [Route("api/v1/ai-model-configs")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class AiModelConfigController : ControllerBase
    {
        private readonly IAiModelConfigService _aiModelConfigService;
        private readonly IMapper _mapper;

        public AiModelConfigController(IAiModelConfigService aiModelConfigService, IMapper mapper)
        {
            _aiModelConfigService = aiModelConfigService;
            _mapper = mapper;
        }

        /// <summary>
        /// List AI model/provider configurations.
        /// </summary>
        /// <response code="200">Configurations returned.</response>
        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(typeof(IEnumerable<AiModelConfigResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<AiModelConfigResponseDto>>> GetAll()
        {
            var items = await _aiModelConfigService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<AiModelConfigResponseDto>>(items);
            return Ok(dtos);
        }

        /// <summary>
        /// Get AI model/provider configuration by id.
        /// </summary>
        /// <param name="id">Configuration identifier.</param>
        /// <response code="200">Configuration returned.</response>
        /// <response code="404">Configuration not found.</response>
        [HttpGet("{id}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AiModelConfigResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AiModelConfigResponseDto>> GetById(long id)
        {
            var item = await _aiModelConfigService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<AiModelConfigResponseDto>(item);
            return Ok(dto);
        }

        /// <summary>
        /// Create a new AI model/provider configuration.
        /// </summary>
        /// <param name="itemDto">Create payload.</param>
        /// <response code="201">Configuration created.</response>
        [HttpPost]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AiModelConfigResponseDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<AiModelConfigResponseDto>> Create([FromBody] AiModelConfigCreateDto itemDto)
        {
            var entity = _mapper.Map<AiModelConfig>(itemDto);
            var createdItem = await _aiModelConfigService.AddAsync(entity);
            var responseDto = _mapper.Map<AiModelConfigResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        /// <summary>
        /// Update an existing AI model/provider configuration.
        /// </summary>
        /// <param name="id">Configuration identifier.</param>
        /// <param name="itemDto">Update payload.</param>
        /// <response code="200">Configuration updated.</response>
        /// <response code="404">Configuration not found.</response>
        [HttpPut("{id}")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(AiModelConfigResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<AiModelConfigResponseDto>> Update(long id, [FromBody] AiModelConfigUpdateDto itemDto)
        {
            var existingItem = await _aiModelConfigService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _aiModelConfigService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<AiModelConfigResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        /// <summary>
        /// Delete AI model/provider configuration by id.
        /// </summary>
        /// <param name="id">Configuration identifier.</param>
        /// <response code="204">Configuration deleted.</response>
        /// <response code="404">Configuration not found.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _aiModelConfigService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}






