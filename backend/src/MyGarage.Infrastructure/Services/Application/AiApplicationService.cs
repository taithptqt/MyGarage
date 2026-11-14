using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Ai;
using MyGarage.Application.Ai.DTOs;
using MyGarage.Application.Authorization;
using MyGarage.Common;
using MyGarage.Domain.Rules;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public sealed class AiApplicationService : IAiApplicationService
{
    private readonly IGarageContext _context;
    private readonly IAiAuthorizationService _authService;

    public AiApplicationService(
        IGarageContext context,
        IAiAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync()
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return null;
        }

        try
        {
            return await _context.Database.BeginTransactionAsync();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("SqlServerRetryingExecutionStrategy"))
            {
                return null;
            }
            throw;
        }
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

        var items = await _context.AiForecasts
            .AsNoTracking()
            .OrderByDescending(f => f.PredictedDate)
            .ThenByDescending(f => f.Id)
            .ToListAsync(ct);

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

        var item = await _context.AiForecasts
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

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

        var vehicleExists = await _context.Vehicles
            .AsNoTracking()
            .AnyAsync(v => v.Id == command.VehicleId && v.IsDeleted != true, ct);
        if (!vehicleExists)
        {
            throw new InvalidOperationException("Phương tiện dự báo không tồn tại.");
        }

        var now = VietnamTime.Now;
        AiForecast entity = null!;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                entity = new AiForecast
                {
                    VehicleId = command.VehicleId,
                    Type = command.Type.Trim(),
                    PredictedDate = command.PredictedDate,
                    ConfidenceScore = command.ConfidenceScore,
                    CreatedAt = now
                };

                _context.AiForecasts.Add(entity);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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

        var entity = await _context.AiForecasts.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (entity == null)
        {
            return null;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                entity.VehicleId = command.VehicleId;
                entity.Type = command.Type.Trim();
                entity.PredictedDate = command.PredictedDate;
                entity.ConfidenceScore = command.ConfidenceScore;

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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

        var items = await _context.AiKnowledgeBases
            .AsNoTracking()
            .OrderByDescending(k => k.LastUpdated)
            .ThenByDescending(k => k.Id)
            .ToListAsync(ct);

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

        var item = await _context.AiKnowledgeBases
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Id == id, ct);

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

        var now = VietnamTime.Now;
        AiKnowledgeBase entity = null!;

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                entity = new AiKnowledgeBase
                {
                    Topic = command.Topic.Trim(),
                    Content = command.Content.Trim(),
                    SourceUrl = string.IsNullOrWhiteSpace(command.SourceUrl) ? null : command.SourceUrl.Trim(),
                    IsDynamic = command.IsDynamic ?? false,
                    LastUpdated = now
                };

                _context.AiKnowledgeBases.Add(entity);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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

        var entity = await _context.AiKnowledgeBases.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (entity == null)
        {
            return null;
        }

        var now = VietnamTime.Now;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                entity.Topic = command.Topic.Trim();
                entity.Content = command.Content.Trim();
                entity.SourceUrl = string.IsNullOrWhiteSpace(command.SourceUrl) ? null : command.SourceUrl.Trim();
                if (command.IsDynamic.HasValue)
                {
                    entity.IsDynamic = command.IsDynamic.Value;
                }
                entity.LastUpdated = now;

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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

        var items = await _context.AiModelConfigs
            .AsNoTracking()
            .OrderByDescending(m => m.IsActive ?? false)
            .ThenByDescending(m => m.Id)
            .ToListAsync(ct);

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

        var item = await _context.AiModelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, ct);

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

        AiModelConfig entity = null!;
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                entity = new AiModelConfig
                {
                    Provider = command.Provider.Trim().ToUpperInvariant(),
                    ModelName = command.ModelName.Trim(),
                    ApiKey = string.IsNullOrWhiteSpace(command.ApiKey) ? null : command.ApiKey.Trim(),
                    MaxTokens = command.MaxTokens,
                    Temperature = command.Temperature,
                    IsActive = command.IsActive
                };

                _context.AiModelConfigs.Add(entity);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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

        var entity = await _context.AiModelConfigs.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (entity == null)
        {
            return null;
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                entity.Provider = command.Provider.Trim().ToUpperInvariant();
                entity.ModelName = command.ModelName.Trim();
                if (!string.IsNullOrWhiteSpace(command.ApiKey))
                {
                    entity.ApiKey = command.ApiKey.Trim();
                }
                entity.MaxTokens = command.MaxTokens;
                entity.Temperature = command.Temperature;
                entity.IsActive = command.IsActive;

                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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
        var entity = await _context.AiSmartDispatchings.FirstOrDefaultAsync(d => d.Id == dispatchId, ct);
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

        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                entity.Status = targetStatus;
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });

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
