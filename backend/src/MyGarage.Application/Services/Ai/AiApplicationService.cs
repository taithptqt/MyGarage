using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Ai;
using MyGarage.Application.Ai.DTOs;
using MyGarage.Application.Authorization;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class AiApplicationService : IAiApplicationService
{
    private readonly IAiApplicationStore _store;
    private readonly IAiAuthorizationService _authService;

    public AiApplicationService(
        IAiApplicationStore store,
        IAiAuthorizationService authService)
    {
        _store = store;
        _authService = authService;
    }

    // =========================================================================
    // 1. AI Forecast Operations
    // =========================================================================

    public async Task<IReadOnlyList<AiForecastItemDto>> GetForecastsAsync(
        long? branchId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessAiForecastsAsync(branchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền truy cập danh sách dự báo AI.");
        }

        var items = await _store.GetForecastsAsync(ct);
        return items.Select(MapForecastDto).ToList();
    }

    public async Task<AiForecastItemDto?> GetForecastByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessAiForecastsAsync(null, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết dự báo AI.");
        }

        var item = await _store.GetForecastByIdAsync(id, track: false, ct);
        return item == null ? null : MapForecastDto(item);
    }

    public async Task<AiForecastItemDto> CreateForecastAsync(
        AiForecastUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessAiForecastsAsync(command.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo dự báo AI.");
        }

        ValidateForecastCommand(command);

        if (!await _store.VehicleExistsAsync(command.VehicleId, ct))
        {
            throw new InvalidOperationException("Phương tiện dự báo không tồn tại.");
        }

        var entity = new AiForecast
        {
            VehicleId = command.VehicleId,
            Type = command.Type.Trim(),
            PredictedDate = command.PredictedDate,
            ConfidenceScore = command.ConfidenceScore,
            CreatedAt = VietnamTime.Now
        };

        await _store.AddForecastAsync(entity, ct);
        return MapForecastDto(entity);
    }

    public async Task<AiForecastItemDto?> UpdateForecastAsync(
        long id,
        AiForecastUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanAccessAiForecastsAsync(command.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền cập nhật dự báo AI.");
        }

        ValidateForecastCommand(command);

        var entity = await _store.GetForecastByIdAsync(id, track: true, ct);
        if (entity == null)
        {
            return null;
        }

        entity.VehicleId = command.VehicleId;
        entity.Type = command.Type.Trim();
        entity.PredictedDate = command.PredictedDate;
        entity.ConfidenceScore = command.ConfidenceScore;

        await _store.SaveForecastAsync(entity, ct);
        return MapForecastDto(entity);
    }

    private static void ValidateForecastCommand(AiForecastUpsertCommand command)
    {
        if (command.VehicleId <= 0)
        {
            throw new ArgumentException("Mã phương tiện không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(command.Type))
        {
            throw new ArgumentException("Loại dự báo không được để trống.");
        }

        if (!AiDomainRules.IsValidConfidenceScore(command.ConfidenceScore))
        {
            throw new ArgumentException("Điểm tin cậy (ConfidenceScore) phải nằm trong khoảng 0 đến 1.");
        }
    }

    private static AiForecastItemDto MapForecastDto(AiForecast f) =>
        new(f.Id, f.VehicleId, f.Type, f.PredictedDate, f.ConfidenceScore, f.CreatedAt);

    // =========================================================================
    // 2. AI Knowledge Base Operations
    // =========================================================================

    public async Task<IReadOnlyList<AiKnowledgeBaseItemDto>> GetKnowledgeEntriesAsync(
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiKnowledgeBaseAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền quản lý cơ sở tri thức AI.");
        }

        var items = await _store.GetKnowledgeEntriesAsync(ct);
        return items.Select(MapKnowledgeDto).ToList();
    }

    public async Task<AiKnowledgeBaseItemDto?> GetKnowledgeEntryByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiKnowledgeBaseAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết cơ sở tri thức AI.");
        }

        var item = await _store.GetKnowledgeEntryByIdAsync(id, track: false, ct);
        return item == null ? null : MapKnowledgeDto(item);
    }

    public async Task<AiKnowledgeBaseItemDto> CreateKnowledgeEntryAsync(
        AiKnowledgeBaseUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiKnowledgeBaseAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo mục tri thức AI.");
        }

        var validation = AiDomainRules.ValidateKnowledgeEntry(command.Topic, command.Content, command.Category ?? "GENERAL");
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error ?? "Dữ liệu tri thức AI không hợp lệ.");
        }

        var entity = new AiKnowledgeBase
        {
            Topic = command.Topic.Trim(),
            Content = command.Content.Trim(),
            SourceUrl = string.IsNullOrWhiteSpace(command.SourceUrl) ? null : command.SourceUrl.Trim(),
            IsDynamic = command.IsDynamic ?? false,
            LastUpdated = VietnamTime.Now
        };

        await _store.AddKnowledgeEntryAsync(entity, ct);
        return MapKnowledgeDto(entity);
    }

    public async Task<AiKnowledgeBaseItemDto?> UpdateKnowledgeEntryAsync(
        long id,
        AiKnowledgeBaseUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiKnowledgeBaseAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền cập nhật mục tri thức AI.");
        }

        var validation = AiDomainRules.ValidateKnowledgeEntry(command.Topic, command.Content, command.Category ?? "GENERAL");
        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error ?? "Dữ liệu tri thức AI không hợp lệ.");
        }

        var entity = await _store.GetKnowledgeEntryByIdAsync(id, track: true, ct);
        if (entity == null)
        {
            return null;
        }

        entity.Topic = command.Topic.Trim();
        entity.Content = command.Content.Trim();
        entity.SourceUrl = string.IsNullOrWhiteSpace(command.SourceUrl) ? null : command.SourceUrl.Trim();
        if (command.IsDynamic.HasValue)
        {
            entity.IsDynamic = command.IsDynamic.Value;
        }
        entity.LastUpdated = VietnamTime.Now;

        await _store.SaveKnowledgeEntryAsync(entity, ct);
        return MapKnowledgeDto(entity);
    }

    private static AiKnowledgeBaseItemDto MapKnowledgeDto(AiKnowledgeBase k) =>
        new(k.Id, k.Topic, k.Content, k.SourceUrl, k.IsDynamic ?? false, k.LastUpdated);

    // =========================================================================
    // 3. AI Model Config Operations
    // =========================================================================

    public async Task<IReadOnlyList<AiModelConfigDto>> GetModelConfigsAsync(
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiModelConfigsAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền quản lý cấu hình mô hình AI.");
        }

        var items = await _store.GetModelConfigsAsync(ct);
        return items.Select(MapModelConfigDto).ToList();
    }

    public async Task<AiModelConfigDto?> GetModelConfigByIdAsync(
        long id,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiModelConfigsAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền xem chi tiết cấu hình mô hình AI.");
        }

        var item = await _store.GetModelConfigByIdAsync(id, track: false, ct);
        return item == null ? null : MapModelConfigDto(item);
    }

    public async Task<AiModelConfigDto> CreateModelConfigAsync(
        AiModelConfigUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiModelConfigsAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền tạo cấu hình mô hình AI.");
        }

        var validation = AiDomainRules.ValidateModelConfig(
            command.Provider,
            command.ModelName,
            command.Temperature,
            command.MaxTokens);

        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error ?? "Cấu hình mô hình AI không hợp lệ.");
        }

        var entity = new AiModelConfig
        {
            Provider = command.Provider.Trim().ToUpperInvariant(),
            ModelName = command.ModelName.Trim(),
            ApiKey = string.IsNullOrWhiteSpace(command.ApiKey) ? null : command.ApiKey.Trim(),
            MaxTokens = command.MaxTokens,
            Temperature = command.Temperature,
            IsActive = command.IsActive
        };

        await _store.AddModelConfigAsync(entity, ct);
        return MapModelConfigDto(entity);
    }

    public async Task<AiModelConfigDto?> UpdateModelConfigAsync(
        long id,
        AiModelConfigUpsertCommand command,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        if (!await _authService.CanManageAiModelConfigsAsync(actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền cập nhật cấu hình mô hình AI.");
        }

        var validation = AiDomainRules.ValidateModelConfig(
            command.Provider,
            command.ModelName,
            command.Temperature,
            command.MaxTokens);

        if (!validation.IsValid)
        {
            throw new ArgumentException(validation.Error ?? "Cấu hình mô hình AI không hợp lệ.");
        }

        var entity = await _store.GetModelConfigByIdAsync(id, track: true, ct);
        if (entity == null)
        {
            return null;
        }

        entity.Provider = command.Provider.Trim().ToUpperInvariant();
        entity.ModelName = command.ModelName.Trim();
        if (!string.IsNullOrWhiteSpace(command.ApiKey))
        {
            entity.ApiKey = command.ApiKey.Trim();
        }
        entity.MaxTokens = command.MaxTokens;
        entity.Temperature = command.Temperature;
        entity.IsActive = command.IsActive;

        await _store.SaveModelConfigAsync(entity, ct);
        return MapModelConfigDto(entity);
    }

    private static AiModelConfigDto MapModelConfigDto(AiModelConfig m) =>
        new(
            m.Id,
            m.Provider,
            m.ModelName,
            !string.IsNullOrWhiteSpace(m.ApiKey),
            AiDomainRules.MaskApiKey(m.ApiKey),
            m.MaxTokens,
            m.Temperature,
            m.IsActive ?? false
        );

    // =========================================================================
    // 4. AI Smart Dispatching Operations
    // =========================================================================

    public Task<AiSmartDispatchItemDto?> ApplyDispatchSuggestionAsync(
        long dispatchId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        return TransitionDispatchStatusInternalAsync(dispatchId, AiDomainRules.DispatchStatusApplied, actor, ct);
    }

    public Task<AiSmartDispatchItemDto?> RejectDispatchSuggestionAsync(
        long dispatchId,
        UserActorContext actor,
        CancellationToken ct = default)
    {
        return TransitionDispatchStatusInternalAsync(dispatchId, AiDomainRules.DispatchStatusRejected, actor, ct);
    }

    private async Task<AiSmartDispatchItemDto?> TransitionDispatchStatusInternalAsync(
        long dispatchId,
        string targetStatus,
        UserActorContext actor,
        CancellationToken ct)
    {
        var entity = await _store.GetSmartDispatchByIdAsync(dispatchId, ct);
        if (entity == null)
        {
            return null;
        }

        if (!await _authService.CanAccessSmartDispatchingAsync(entity.BranchId, actor, ct))
        {
            throw new UnauthorizedAccessException("Không có quyền thao tác điều phối thông minh tại chi nhánh này.");
        }

        if (!AiDomainRules.CanTransitionDispatchStatus(entity.Status, targetStatus))
        {
            throw new InvalidOperationException(
                $"Không thể chuyển trạng thái điều phối từ '{entity.Status ?? AiDomainRules.DispatchStatusPending}' sang '{targetStatus}'.");
        }

        entity.Status = targetStatus;
        await _store.SaveSmartDispatchAsync(entity, ct);

        return new AiSmartDispatchItemDto(
            entity.Id,
            entity.BranchId,
            entity.Date,
            entity.Recommendation,
            entity.EfficiencyScore,
            entity.Status ?? targetStatus,
            entity.CreatedAt
        );
    }
}
