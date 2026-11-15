using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Models;

namespace MyGarage.Application.Ai;

public interface IAiApplicationStore
{
    Task<IReadOnlyList<AiForecast>> GetForecastsAsync(CancellationToken ct = default);
    Task<AiForecast?> GetForecastByIdAsync(long id, bool track = false, CancellationToken ct = default);
    Task<bool> VehicleExistsAsync(long vehicleId, CancellationToken ct = default);
    Task AddForecastAsync(AiForecast entity, CancellationToken ct = default);
    Task SaveForecastAsync(AiForecast entity, CancellationToken ct = default);

    Task<IReadOnlyList<AiKnowledgeBase>> GetKnowledgeEntriesAsync(CancellationToken ct = default);
    Task<AiKnowledgeBase?> GetKnowledgeEntryByIdAsync(long id, bool track = false, CancellationToken ct = default);
    Task AddKnowledgeEntryAsync(AiKnowledgeBase entity, CancellationToken ct = default);
    Task SaveKnowledgeEntryAsync(AiKnowledgeBase entity, CancellationToken ct = default);

    Task<IReadOnlyList<AiModelConfig>> GetModelConfigsAsync(CancellationToken ct = default);
    Task<AiModelConfig?> GetModelConfigByIdAsync(long id, bool track = false, CancellationToken ct = default);
    Task AddModelConfigAsync(AiModelConfig entity, CancellationToken ct = default);
    Task SaveModelConfigAsync(AiModelConfig entity, CancellationToken ct = default);

    Task<AiSmartDispatching?> GetSmartDispatchByIdAsync(long dispatchId, CancellationToken ct = default);
    Task SaveSmartDispatchAsync(AiSmartDispatching entity, CancellationToken ct = default);
}
