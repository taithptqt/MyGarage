using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/model-specific-rules")]
[Route("api/[controller]")]
    [ApiController]
    public class ModelSpecificRuleController : ControllerBase
    {
        private readonly IModelSpecificRuleService _modelSpecificRuleService;

        public ModelSpecificRuleController(IModelSpecificRuleService modelSpecificRuleService)
        {
            _modelSpecificRuleService = modelSpecificRuleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ModelSpecificRule>>> GetAll()
        {
            var items = await _modelSpecificRuleService.GetAllAsync();
            return Ok(items);
        }

        [HttpGet("{modelId}/{productId}")]
        public async Task<ActionResult<ModelSpecificRule>> GetById(long modelId, long productId)
        {
            var item = await _modelSpecificRuleService.GetByIdAsync(new object[] { modelId, productId });
            if (item == null)
            {
                return NotFound();
            }
            return Ok(item);
        }

        [HttpPost]
        public async Task<ActionResult<ModelSpecificRule>> Create([FromBody] ModelSpecificRule item)
        {
            var createdItem = await _modelSpecificRuleService.AddAsync(item);
            return CreatedAtAction(nameof(GetById), new { modelId = createdItem.ModelId, productId = createdItem.ProductId }, createdItem);
        }

        [HttpPut("{modelId}/{productId}")]
        public async Task<ActionResult<ModelSpecificRule>> Update(long modelId, long productId, [FromBody] ModelSpecificRule item)
        {
            item.ModelId = modelId;
            item.ProductId = productId;
            var updatedItem = await _modelSpecificRuleService.UpdateAsync(new object[] { modelId, productId }, item);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            return Ok(updatedItem);
        }

        [HttpDelete("{modelId}/{productId}")]
        public async Task<IActionResult> Delete(long modelId, long productId)
        {
            var result = await _modelSpecificRuleService.DeleteAsync(new object[] { modelId, productId });
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}




