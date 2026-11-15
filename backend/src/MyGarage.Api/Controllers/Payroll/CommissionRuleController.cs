using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Controllers
{
    [Route("api/v1/commission-rules")]
    [ApiController]
    [Authorize(Policy = "AdminStaffOnly")]
    public class CommissionRuleController : ControllerBase
    {
        private const string TargetTypeServiceAdvisor = "SERVICE_ADVISOR";
        private static readonly HashSet<string> AllowedTargetTypes = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "SERVICE",
            "PART",
            "STAFF_TIER",
            "SERVICE_ADVISOR"
        };

        private static readonly HashSet<string> AllowedCommissionTypes = new(System.StringComparer.OrdinalIgnoreCase)
        {
            "PERCENT",
            "FIXED"
        };

        private readonly ICommissionRuleService _commissionRuleService;
        private readonly ICommissionRuleDataService _dataService;

        public CommissionRuleController(ICommissionRuleService commissionRuleService, ICommissionRuleDataService dataService)
        {
            _commissionRuleService = commissionRuleService;
            _dataService = dataService;
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
            var validationError = await ValidateCommissionRuleInputAsync(
                itemDto.BranchId,
                itemDto.StaffTierId,
                itemDto.TargetType,
                itemDto.CommissionType,
                itemDto.CommissionValue);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return this.BadRequestProblem(validationError);
            }

            var entity = _commissionRuleService.MapToEntity(itemDto);
            entity.TargetType = NormalizeTargetType(itemDto.TargetType);
            entity.CommissionType = NormalizeCommissionType(itemDto.CommissionType);
            entity.IsActive ??= true;
            var createdItem = await _commissionRuleService.AddAsync(entity);
            var responseDto = _commissionRuleService.MapToDto(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<CommissionRuleResponseDto>> Update(long id, [FromBody] CommissionRuleUpdateDto itemDto)
        {
            var existingItem = await _commissionRuleService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            var validationError = await ValidateCommissionRuleInputAsync(
                itemDto.BranchId,
                itemDto.StaffTierId,
                itemDto.TargetType,
                itemDto.CommissionType,
                itemDto.CommissionValue);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return this.BadRequestProblem(validationError);
            }

            _commissionRuleService.MapUpdate(itemDto, existingItem);
            existingItem.TargetType = NormalizeTargetType(itemDto.TargetType);
            existingItem.CommissionType = NormalizeCommissionType(itemDto.CommissionType);
            var updatedItem = await _commissionRuleService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _commissionRuleService.MapToDto(updatedItem);
            return Ok(responseDto);
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
            var rows = await _dataService.GetServiceAdvisorRulesAsync(branchId, staffTierId, isActive, HttpContext.RequestAborted);
            return Ok(new
            {
                total = rows.Count,
                items = rows
            });
        }

        [HttpPut("service-advisor")]
        public async Task<IActionResult> UpsertServiceAdvisorRule([FromBody] ServiceAdvisorCommissionRuleUpsertRequestDto? request)
        {
            if (request == null)
            {
                return this.BadRequestProblem("Payload is required.");
            }

            var validationError = await ValidateCommissionRuleInputAsync(
                request.BranchId,
                request.StaffTierId,
                TargetTypeServiceAdvisor,
                request.CommissionType,
                request.CommissionValue);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return this.BadRequestProblem(validationError);
            }

            var (rule, created) = await _dataService.UpsertServiceAdvisorRuleAsync(
                request.BranchId,
                request.StaffTierId,
                request.TargetId,
                NormalizeCommissionType(request.CommissionType),
                request.CommissionValue!.Value,
                request.IsActive,
                HttpContext.RequestAborted);

            return Ok(new
            {
                message = created ? "Service advisor commission rule created." : "Service advisor commission rule updated.",
                rule = _commissionRuleService.MapToDto(rule)
            });
        }

        private async Task<string?> ValidateCommissionRuleInputAsync(
            long branchId,
            long staffTierId,
            string? targetType,
            string? commissionType,
            decimal? commissionValue)
        {
            if (branchId <= 0) return "branchId must be greater than 0.";
            if (staffTierId <= 0) return "staffTierId must be greater than 0.";
            if (!commissionValue.HasValue || commissionValue.Value <= 0m) return "commissionValue must be greater than 0.";

            var normalizedTargetType = NormalizeTargetType(targetType);
            if (!AllowedTargetTypes.Contains(normalizedTargetType))
            {
                return "targetType is invalid. Allowed: SERVICE, PART, STAFF_TIER, SERVICE_ADVISOR.";
            }

            var normalizedCommissionType = NormalizeCommissionType(commissionType);
            if (!AllowedCommissionTypes.Contains(normalizedCommissionType))
            {
                return "commissionType is invalid. Allowed: PERCENT, FIXED.";
            }

            if (normalizedCommissionType == "PERCENT" && commissionValue.Value > 100m)
            {
                return "commissionValue cannot exceed 100 for PERCENT type.";
            }

            if (!await _dataService.BranchExistsAsync(branchId, HttpContext.RequestAborted))
            {
                return $"Branch #{branchId} not found.";
            }

            if (!await _dataService.StaffTierExistsAsync(staffTierId, HttpContext.RequestAborted))
            {
                return $"Staff tier #{staffTierId} not found.";
            }

            return null;
        }

        private static string NormalizeTargetType(string? value)
        {
            var normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (normalized is "SERVICEADVISOR" or "SOADVISOR" or "ADVISOR")
            {
                return TargetTypeServiceAdvisor;
            }

            return normalized;
        }

        private static string NormalizeCommissionType(string? value)
            => string.IsNullOrWhiteSpace(value) ? "PERCENT" : value.Trim().ToUpperInvariant();
    }
}






