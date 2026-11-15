using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Models;

namespace MyGarage.Interfaces
{
    public interface IModelSpecificRuleService : IGenericService<ModelSpecificRule>
    {
        Task<IEnumerable<ModelSpecificRuleResponseDto>> GetAllDtosAsync(CancellationToken ct = default);
        Task<ModelSpecificRuleResponseDto?> GetDtoByKeyAsync(long modelId, long productId, CancellationToken ct = default);
        Task<ModelSpecificRuleResponseDto> CreateDtoAsync(ModelSpecificRuleCreateDto dto, CancellationToken ct = default);
        Task<ModelSpecificRuleResponseDto?> UpdateDtoAsync(long modelId, long productId, ModelSpecificRuleUpdateDto dto, CancellationToken ct = default);
        Task<bool> DeleteByKeyAsync(long modelId, long productId, CancellationToken ct = default);
    }
}
