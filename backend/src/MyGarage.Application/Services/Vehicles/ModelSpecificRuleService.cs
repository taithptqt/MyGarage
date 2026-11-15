using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services
{
    public class ModelSpecificRuleService : GenericService<ModelSpecificRule>, IModelSpecificRuleService
    {
        public ModelSpecificRuleService(IModelSpecificRuleRepository repository) : base(repository)
        {
        }

        public async Task<IEnumerable<ModelSpecificRuleResponseDto>> GetAllDtosAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var items = await GetAllAsync();
            return items.Select(MapToDto).ToList();
        }

        public async Task<ModelSpecificRuleResponseDto?> GetDtoByKeyAsync(long modelId, long productId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            var item = await GetByIdAsync(BuildCompositeKey(modelId, productId));
            return item == null ? null : MapToDto(item);
        }

        public async Task<ModelSpecificRuleResponseDto> CreateDtoAsync(ModelSpecificRuleCreateDto dto, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(dto);

            var entity = ToEntity(dto);
            var created = await AddAsync(entity);
            return MapToDto(created);
        }

        public async Task<ModelSpecificRuleResponseDto?> UpdateDtoAsync(
            long modelId,
            long productId,
            ModelSpecificRuleUpdateDto dto,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(dto);

            var entity = ToNormalizedEntity(modelId, productId, dto);
            var updated = await UpdateAsync(BuildCompositeKey(modelId, productId), entity);
            return updated == null ? null : MapToDto(updated);
        }

        public Task<bool> DeleteByKeyAsync(long modelId, long productId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return DeleteAsync(BuildCompositeKey(modelId, productId));
        }

        internal static object[] BuildCompositeKey(long modelId, long productId)
            => new object[] { modelId, productId };

        internal static ModelSpecificRule ToEntity(ModelSpecificRuleCreateDto dto)
            => new()
            {
                ModelId = dto.ModelId,
                ProductId = dto.ProductId,
                StandardCapacity = dto.StandardCapacity,
                Tolerance = dto.Tolerance
            };

        internal static ModelSpecificRule ToNormalizedEntity(long modelId, long productId, ModelSpecificRuleUpdateDto dto)
            => new()
            {
                ModelId = modelId,
                ProductId = productId,
                StandardCapacity = dto.StandardCapacity,
                Tolerance = dto.Tolerance
            };

        internal static ModelSpecificRuleResponseDto MapToDto(ModelSpecificRule item)
            => new()
            {
                ModelId = item.ModelId,
                ProductId = item.ProductId,
                StandardCapacity = item.StandardCapacity,
                Tolerance = item.Tolerance
            };
    }
}
