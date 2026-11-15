using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/v1/model-specific-rules")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class ModelSpecificRuleController : ControllerBase
    {
        private readonly IModelSpecificRuleService _modelSpecificRuleService;

        public ModelSpecificRuleController(IModelSpecificRuleService modelSpecificRuleService)
        {
            _modelSpecificRuleService = modelSpecificRuleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ModelSpecificRuleResponseDto>>> GetAll()
        {
            var items = await _modelSpecificRuleService.GetAllDtosAsync(HttpContext.RequestAborted);
            return Ok(items);
        }

        [HttpGet("{modelId}/{productId}")]
        public async Task<ActionResult<ModelSpecificRuleResponseDto>> GetById(long modelId, long productId)
        {
            var item = await _modelSpecificRuleService.GetDtoByKeyAsync(modelId, productId, HttpContext.RequestAborted);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<ModelSpecificRuleResponseDto>> Create([FromBody] ModelSpecificRuleCreateDto itemDto)
        {
            var createdItem = await _modelSpecificRuleService.CreateDtoAsync(itemDto, HttpContext.RequestAborted);
            return CreatedAtAction(
                nameof(GetById),
                new { modelId = createdItem.ModelId, productId = createdItem.ProductId },
                createdItem);
        }

        [HttpPut("{modelId}/{productId}")]
        public async Task<ActionResult<ModelSpecificRuleResponseDto>> Update(
            long modelId,
            long productId,
            [FromBody] ModelSpecificRuleUpdateDto itemDto)
        {
            var updatedItem = await _modelSpecificRuleService.UpdateDtoAsync(
                modelId,
                productId,
                itemDto,
                HttpContext.RequestAborted);

            if (updatedItem == null)
            {
                return NotFound();
            }

            return Ok(updatedItem);
        }

        [HttpDelete("{modelId}/{productId}")]
        public async Task<IActionResult> Delete(long modelId, long productId)
        {
            var result = await _modelSpecificRuleService.DeleteByKeyAsync(modelId, productId, HttpContext.RequestAborted);
            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
