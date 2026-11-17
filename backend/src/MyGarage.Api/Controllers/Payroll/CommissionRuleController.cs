using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/v1/commission-rules")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class CommissionRuleController : ControllerBase
    {
        private readonly ICommissionRuleService _commissionRuleService;

        public CommissionRuleController(ICommissionRuleService commissionRuleService)
        {
            _commissionRuleService = commissionRuleService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommissionRuleResponseDto>>> GetAll()
        {
            var dtos = await _commissionRuleService.GetAllDtosAsync();
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommissionRuleResponseDto>> GetById(long id)
        {
            var dto = await _commissionRuleService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<CommissionRuleResponseDto>> Create([FromBody] CommissionRuleCreateDto itemDto)
        {
            var result = await _commissionRuleService.CreateRuleAsync(itemDto, HttpContext.RequestAborted);
            if (result.Status == CommissionRuleMutationStatus.BadRequest)
            {
                return this.BadRequestProblem(result.ErrorMessage ?? "Invalid commission rule payload.");
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CommissionRuleResponseDto>> Update(long id, [FromBody] CommissionRuleUpdateDto itemDto)
        {
            var result = await _commissionRuleService.UpdateRuleAsync(id, itemDto, HttpContext.RequestAborted);
            return result.Status switch
            {
                CommissionRuleMutationStatus.NotFound => NotFound(),
                CommissionRuleMutationStatus.BadRequest => this.BadRequestProblem(result.ErrorMessage ?? "Invalid commission rule payload."),
                _ => Ok(result.Data)
            };
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _commissionRuleService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpGet("service-advisor")]
        public async Task<IActionResult> GetServiceAdvisorRules(
            [FromQuery] long? branchId = null,
            [FromQuery] long? staffTierId = null,
            [FromQuery] bool? isActive = true)
        {
            var rows = await _commissionRuleService.GetServiceAdvisorRulesAsync(
                branchId,
                staffTierId,
                isActive,
                HttpContext.RequestAborted);
            return Ok(new
            {
                total = rows.Count,
                items = rows
            });
        }

        [HttpPut("service-advisor")]
        public async Task<IActionResult> UpsertServiceAdvisorRule([FromBody] ServiceAdvisorCommissionRuleUpsertRequestDto? request)
        {
            var result = await _commissionRuleService.UpsertServiceAdvisorRuleAsync(request, HttpContext.RequestAborted);
            if (result.Status == CommissionRuleMutationStatus.BadRequest)
            {
                return this.BadRequestProblem(result.ErrorMessage ?? "Invalid service advisor commission rule payload.");
            }

            return Ok(new
            {
                message = result.Data.Created
                    ? "Service advisor commission rule created."
                    : "Service advisor commission rule updated.",
                rule = result.Data.Rule
            });
        }
    }
}
