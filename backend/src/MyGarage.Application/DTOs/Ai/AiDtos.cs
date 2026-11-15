using System;

namespace MyGarage.Application.Ai.DTOs;

public sealed record AiForecastUpsertCommand(
    long VehicleId,
    string Type,
    DateOnly PredictedDate,
    decimal? ConfidenceScore,
    long? BranchId = null
);

public sealed record AiForecastItemDto(
    long Id,
    long VehicleId,
    string Type,
    DateOnly PredictedDate,
    decimal? ConfidenceScore,
    DateTime? CreatedAt
);

public sealed record AiKnowledgeBaseUpsertCommand(
    string Topic,
    string Content,
    string? Category = "GENERAL",
    string? SourceUrl = null,
    bool? IsDynamic = false
);

public sealed record AiKnowledgeBaseItemDto(
    long Id,
    string Topic,
    string Content,
    string? SourceUrl,
    bool IsDynamic,
    DateTime? LastUpdated
);

public sealed record AiModelConfigUpsertCommand(
    string Provider,
    string ModelName,
    string? ApiKey,
    int? MaxTokens,
    decimal? Temperature,
    bool IsActive = true
);

public sealed record AiModelConfigDto(
    long Id,
    string Provider,
    string ModelName,
    bool HasApiKey,
    string? MaskedApiKey,
    int? MaxTokens,
    decimal? Temperature,
    bool IsActive
);

public sealed record AiSmartDispatchItemDto(
    long Id,
    long BranchId,
    DateOnly Date,
    string? Recommendation,
    decimal? EfficiencyScore,
    string Status,
    DateTime? CreatedAt
);
