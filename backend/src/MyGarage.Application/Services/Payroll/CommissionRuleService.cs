using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class CommissionRuleService : GenericService<CommissionRule>, ICommissionRuleService
    {
        private const string TargetTypeServiceAdvisor = "SERVICE_ADVISOR";
        private static readonly HashSet<string> AllowedTargetTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "SERVICE",
            "PART",
            "STAFF_TIER",
            "SERVICE_ADVISOR"
        };

        private static readonly HashSet<string> AllowedCommissionTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "PERCENT",
            "FIXED"
        };

        private readonly ICommissionRuleDataService _dataService;
        private readonly IMapper _mapper;

        public CommissionRuleService(
            ICommissionRuleRepository repository,
            ICommissionRuleDataService dataService,
            IMapper mapper) : base(repository)
        {
            _dataService = dataService;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CommissionRuleResponseDto>> GetAllDtosAsync()
        {
            var items = await GetAllAsync();
            return _mapper.Map<IEnumerable<CommissionRuleResponseDto>>(items);
        }

        public async Task<CommissionRuleResponseDto?> GetDtoByIdAsync(long id)
        {
            var item = await GetByIdAsync(id);
            return item == null ? null : _mapper.Map<CommissionRuleResponseDto>(item);
        }

        public CommissionRuleResponseDto MapToDto(CommissionRule item)
            => _mapper.Map<CommissionRuleResponseDto>(item);

        public CommissionRule MapToEntity(CommissionRuleCreateDto itemDto)
            => _mapper.Map<CommissionRule>(itemDto);

        public void MapUpdate(CommissionRuleUpdateDto itemDto, CommissionRule existingItem)
            => _mapper.Map(itemDto, existingItem);

        public async Task<CommissionRuleCommandResult<CommissionRuleResponseDto>> CreateRuleAsync(
            CommissionRuleCreateDto itemDto,
            CancellationToken ct = default)
        {
            var validationError = await ValidateCommissionRuleInputAsync(
                itemDto.BranchId,
                itemDto.StaffTierId,
                itemDto.TargetType,
                itemDto.CommissionType,
                itemDto.CommissionValue,
                ct);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return new CommissionRuleCommandResult<CommissionRuleResponseDto>(
                    CommissionRuleMutationStatus.BadRequest,
                    ErrorMessage: validationError);
            }

            var entity = MapToEntity(itemDto);
            entity.TargetType = NormalizeTargetType(itemDto.TargetType);
            entity.CommissionType = NormalizeCommissionType(itemDto.CommissionType);
            entity.IsActive ??= true;
            var createdItem = await AddAsync(entity);
            return new CommissionRuleCommandResult<CommissionRuleResponseDto>(
                CommissionRuleMutationStatus.Created,
                Data: MapToDto(createdItem));
        }

        public async Task<CommissionRuleCommandResult<CommissionRuleResponseDto>> UpdateRuleAsync(
            long id,
            CommissionRuleUpdateDto itemDto,
            CancellationToken ct = default)
        {
            var existingItem = await GetByIdAsync(id);
            if (existingItem == null)
            {
                return new CommissionRuleCommandResult<CommissionRuleResponseDto>(CommissionRuleMutationStatus.NotFound);
            }

            var validationError = await ValidateCommissionRuleInputAsync(
                itemDto.BranchId,
                itemDto.StaffTierId,
                itemDto.TargetType,
                itemDto.CommissionType,
                itemDto.CommissionValue,
                ct);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return new CommissionRuleCommandResult<CommissionRuleResponseDto>(
                    CommissionRuleMutationStatus.BadRequest,
                    ErrorMessage: validationError);
            }

            MapUpdate(itemDto, existingItem);
            existingItem.TargetType = NormalizeTargetType(itemDto.TargetType);
            existingItem.CommissionType = NormalizeCommissionType(itemDto.CommissionType);
            var updatedItem = await UpdateAsync(id, existingItem);
            if (updatedItem == null)
            {
                return new CommissionRuleCommandResult<CommissionRuleResponseDto>(CommissionRuleMutationStatus.NotFound);
            }

            return new CommissionRuleCommandResult<CommissionRuleResponseDto>(
                CommissionRuleMutationStatus.Ok,
                Data: MapToDto(updatedItem));
        }

        public Task<IReadOnlyList<ServiceAdvisorCommissionRuleRow>> GetServiceAdvisorRulesAsync(
            long? branchId,
            long? staffTierId,
            bool? isActive,
            CancellationToken ct = default)
            => _dataService.GetServiceAdvisorRulesAsync(branchId, staffTierId, isActive, ct);

        public async Task<CommissionRuleCommandResult<(CommissionRuleResponseDto Rule, bool Created)>> UpsertServiceAdvisorRuleAsync(
            ServiceAdvisorCommissionRuleUpsertRequestDto? request,
            CancellationToken ct = default)
        {
            if (request == null)
            {
                return new CommissionRuleCommandResult<(CommissionRuleResponseDto, bool)>(
                    CommissionRuleMutationStatus.BadRequest,
                    ErrorMessage: "Payload is required.");
            }

            var validationError = await ValidateCommissionRuleInputAsync(
                request.BranchId,
                request.StaffTierId,
                TargetTypeServiceAdvisor,
                request.CommissionType,
                request.CommissionValue,
                ct);
            if (!string.IsNullOrWhiteSpace(validationError))
            {
                return new CommissionRuleCommandResult<(CommissionRuleResponseDto, bool)>(
                    CommissionRuleMutationStatus.BadRequest,
                    ErrorMessage: validationError);
            }

            var (rule, created) = await _dataService.UpsertServiceAdvisorRuleAsync(
                request.BranchId,
                request.StaffTierId,
                request.TargetId,
                NormalizeCommissionType(request.CommissionType),
                request.CommissionValue!.Value,
                request.IsActive,
                ct);

            return new CommissionRuleCommandResult<(CommissionRuleResponseDto, bool)>(
                CommissionRuleMutationStatus.Ok,
                Data: (MapToDto(rule), created));
        }

        private async Task<string?> ValidateCommissionRuleInputAsync(
            long branchId,
            long staffTierId,
            string? targetType,
            string? commissionType,
            decimal? commissionValue,
            CancellationToken ct)
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

            if (!await _dataService.BranchExistsAsync(branchId, ct))
            {
                return $"Branch #{branchId} not found.";
            }

            if (!await _dataService.StaffTierExistsAsync(staffTierId, ct))
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
