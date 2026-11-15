using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/ai-model-configs")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class AiModelConfigController : ControllerBase
    {
        private readonly IAiModelConfigService _aiModelConfigService;

        public AiModelConfigController(IAiModelConfigService aiModelConfigService)
        {
            _aiModelConfigService = aiModelConfigService;
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
            var dtos = await _aiModelConfigService.GetAllDtosAsync();
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
            var dto = await _aiModelConfigService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
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
            var responseDto = await _aiModelConfigService.CreateDtoAsync(itemDto);
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
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
            var responseDto = await _aiModelConfigService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
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






