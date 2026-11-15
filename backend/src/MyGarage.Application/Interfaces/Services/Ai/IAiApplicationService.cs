using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Ai.DTOs;
using MyGarage.Application.Authorization;

namespace MyGarage.Application.Ai;

public interface IAiApplicationService
{
    Task<IReadOnlyList<AiForecastItemDto>> GetForecastsAsync(long? branchId, UserActorContext actor, CancellationToken ct = default);
    Task<AiForecastItemDto?> GetForecastByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<AiForecastItemDto> CreateForecastAsync(AiForecastUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<AiForecastItemDto?> UpdateForecastAsync(long id, AiForecastUpsertCommand command, UserActorContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<AiKnowledgeBaseItemDto>> GetKnowledgeEntriesAsync(UserActorContext actor, CancellationToken ct = default);
    Task<AiKnowledgeBaseItemDto?> GetKnowledgeEntryByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<AiKnowledgeBaseItemDto> CreateKnowledgeEntryAsync(AiKnowledgeBaseUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<AiKnowledgeBaseItemDto?> UpdateKnowledgeEntryAsync(long id, AiKnowledgeBaseUpsertCommand command, UserActorContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<AiModelConfigDto>> GetModelConfigsAsync(UserActorContext actor, CancellationToken ct = default);
    Task<AiModelConfigDto?> GetModelConfigByIdAsync(long id, UserActorContext actor, CancellationToken ct = default);
    Task<AiModelConfigDto> CreateModelConfigAsync(AiModelConfigUpsertCommand command, UserActorContext actor, CancellationToken ct = default);
    Task<AiModelConfigDto?> UpdateModelConfigAsync(long id, AiModelConfigUpsertCommand command, UserActorContext actor, CancellationToken ct = default);

    Task<AiSmartDispatchItemDto?> ApplyDispatchSuggestionAsync(long dispatchId, UserActorContext actor, CancellationToken ct = default);
    Task<AiSmartDispatchItemDto?> RejectDispatchSuggestionAsync(long dispatchId, UserActorContext actor, CancellationToken ct = default);
}
