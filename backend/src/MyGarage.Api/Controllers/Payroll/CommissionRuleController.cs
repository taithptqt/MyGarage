using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/commission-rules")]
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
        private readonly IMapper _mapper;
        private readonly IGarageContext _db;

        public CommissionRuleController(ICommissionRuleService commissionRuleService, IMapper mapper, IGarageContext db)
        {
            _commissionRuleService = commissionRuleService;
            _mapper = mapper;
            _db = db;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CommissionRuleResponseDto>>> GetAll()
        {
            var items = await _commissionRuleService.GetAllAsync();
            var dtos = _mapper.Map<IEnumerable<CommissionRuleResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<CommissionRuleResponseDto>> GetById(long id)
        {
            var item = await _commissionRuleService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<CommissionRuleResponseDto>(item);
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
                return BadRequest(new { message = validationError });
            }

            var entity = _mapper.Map<CommissionRule>(itemDto);
            entity.TargetType = NormalizeTargetType(itemDto.TargetType);
            entity.CommissionType = NormalizeCommissionType(itemDto.CommissionType);
            entity.IsActive ??= true;
            var createdItem = await _commissionRuleService.AddAsync(entity);
            var responseDto = _mapper.Map<CommissionRuleResponseDto>(createdItem);
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
                return BadRequest(new { message = validationError });
            }

            _mapper.Map(itemDto, existingItem);
            existingItem.TargetType = NormalizeTargetType(itemDto.TargetType);
            existingItem.CommissionType = NormalizeCommissionType(itemDto.CommissionType);
            var updatedItem = await _commissionRuleService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<CommissionRuleResponseDto>(updatedItem);
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
            var query = _db.CommissionRules
                .AsNoTracking()
                .Where(x => x.IsDeleted != true && x.TargetType == TargetTypeServiceAdvisor);

            if (branchId.HasValue)
            {
                query = query.Where(x => x.BranchId == branchId.Value);
            }

            if (staffTierId.HasValue)
            {
                query = query.Where(x => x.StaffTierId == staffTierId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(x => (x.IsActive ?? true) == isActive.Value);
            }

            var rows = await query
                .OrderBy(x => x.BranchId)
                .ThenBy(x => x.StaffTierId)
                .ThenByDescending(x => x.IsActive)
                .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.BranchId,
                    x.StaffTierId,
                    x.TargetId,
                    x.CommissionType,
                    x.CommissionValue,
                    isActive = x.IsActive ?? true,
                    x.CreatedAt,
                    x.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                total = rows.Count,
                items = rows
            });
        }

        [HttpPost("service-advisor/upsert")]
        public async Task<IActionResult> UpsertServiceAdvisorRule([FromBody] ServiceAdvisorCommissionRuleUpsertRequestDto? request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Payload is required." });
            }

            var validationError = await ValidateCommissionRuleInputAsync(
                request.BranchId,
                request.StaffTierId,
                TargetTypeServiceAdvisor,
                request.CommissionType,
                request.CommissionValue);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return BadRequest(new { message = validationError });
            }

            var existing = await _db.CommissionRules
                .FirstOrDefaultAsync(x =>
                    x.IsDeleted != true &&
                    x.BranchId == request.BranchId &&
                    x.StaffTierId == request.StaffTierId &&
                    x.TargetType == TargetTypeServiceAdvisor &&
                    x.TargetId == request.TargetId);

            var now = VietnamTime.Now;
            if (existing == null)
            {
                var entity = new CommissionRule
                {
                    BranchId = request.BranchId,
                    StaffTierId = request.StaffTierId,
                    TargetType = TargetTypeServiceAdvisor,
                    TargetId = request.TargetId,
                    CommissionType = NormalizeCommissionType(request.CommissionType),
                    CommissionValue = request.CommissionValue!.Value,
                    IsActive = request.IsActive,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.CommissionRules.Add(entity);
                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = "Service advisor commission rule created.",
                    rule = _mapper.Map<CommissionRuleResponseDto>(entity)
                });
            }

            existing.CommissionType = NormalizeCommissionType(request.CommissionType);
            existing.CommissionValue = request.CommissionValue!.Value;
            existing.IsActive = request.IsActive;
            existing.UpdatedAt = now;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Service advisor commission rule updated.",
                rule = _mapper.Map<CommissionRuleResponseDto>(existing)
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

            var branchExists = await _db.Branches
                .AsNoTracking()
                .AnyAsync(x => x.Id == branchId && x.IsDeleted != true);
            if (!branchExists)
            {
                return $"Branch #{branchId} not found.";
            }

            var tierExists = await _db.StaffTiers
                .AsNoTracking()
                .AnyAsync(x => x.Id == staffTierId && x.IsDeleted != true);
            if (!tierExists)
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






