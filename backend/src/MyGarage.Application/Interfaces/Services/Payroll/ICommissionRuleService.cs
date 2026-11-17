using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Queries;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public enum CommissionRuleMutationStatus
    {
        Ok,
        Created,
        NotFound,
        BadRequest
    }

    public sealed record CommissionRuleCommandResult<T>(
        CommissionRuleMutationStatus Status,
        T? Data = default,
        string? ErrorMessage = null);

    public interface ICommissionRuleService : IGenericService<CommissionRule>
    {
        Task<IEnumerable<CommissionRuleResponseDto>> GetAllDtosAsync();
        Task<CommissionRuleResponseDto?> GetDtoByIdAsync(long id);
        CommissionRuleResponseDto MapToDto(CommissionRule item);
        CommissionRule MapToEntity(CommissionRuleCreateDto itemDto);
        void MapUpdate(CommissionRuleUpdateDto itemDto, CommissionRule existingItem);
        Task<CommissionRuleCommandResult<CommissionRuleResponseDto>> CreateRuleAsync(
            CommissionRuleCreateDto itemDto,
            CancellationToken ct = default);
        Task<CommissionRuleCommandResult<CommissionRuleResponseDto>> UpdateRuleAsync(
            long id,
            CommissionRuleUpdateDto itemDto,
            CancellationToken ct = default);
        Task<IReadOnlyList<ServiceAdvisorCommissionRuleRow>> GetServiceAdvisorRulesAsync(
            long? branchId,
            long? staffTierId,
            bool? isActive,
            CancellationToken ct = default);
        Task<CommissionRuleCommandResult<(CommissionRuleResponseDto Rule, bool Created)>> UpsertServiceAdvisorRuleAsync(
            ServiceAdvisorCommissionRuleUpsertRequestDto? request,
            CancellationToken ct = default);
    }
}
