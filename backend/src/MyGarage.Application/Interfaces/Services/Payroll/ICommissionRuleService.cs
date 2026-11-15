using System.Collections.Generic;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface ICommissionRuleService : IGenericService<CommissionRule>
    {
        Task<IEnumerable<CommissionRuleResponseDto>> GetAllDtosAsync();
        Task<CommissionRuleResponseDto?> GetDtoByIdAsync(long id);
        CommissionRuleResponseDto MapToDto(CommissionRule item);
        CommissionRule MapToEntity(CommissionRuleCreateDto itemDto);
        void MapUpdate(CommissionRuleUpdateDto itemDto, CommissionRule existingItem);
    }
}
