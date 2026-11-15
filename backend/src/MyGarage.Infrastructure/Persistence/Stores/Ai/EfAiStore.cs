using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyGarage.Application.Ai;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Infrastructure.Ai;

public sealed class EfAiStore : IAiStore
{
    private readonly IGarageContext _context;

    public EfAiStore(IGarageContext context)
    {
        _context = context;
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

    private async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync();
            try
            {
                await action(ct);
                await _context.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);
            }
            catch
            {
                if (tx != null) await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<AiForecast>> GetForecastsAsync(CancellationToken ct = default)
    {
        return await _context.AiForecasts
            .AsNoTracking()
            .OrderByDescending(f => f.PredictedDate)
            .ThenByDescending(f => f.Id)
            .ToListAsync(ct);
    }

    public async Task<AiForecast?> GetForecastByIdAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var q = track ? _context.AiForecasts : _context.AiForecasts.AsNoTracking();
        return await q.FirstOrDefaultAsync(f => f.Id == id, ct);
    }

    public Task<bool> VehicleExistsAsync(long vehicleId, CancellationToken ct = default)
    {
        return _context.Vehicles
            .AsNoTracking()
            .AnyAsync(v => v.Id == vehicleId && v.IsDeleted != true, ct);
    }

    public Task AddForecastAsync(AiForecast entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            _context.AiForecasts.Add(entity);
            return Task.CompletedTask;
        }, ct);
    }

    public Task SaveForecastAsync(AiForecast entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public async Task<IReadOnlyList<AiKnowledgeBase>> GetKnowledgeEntriesAsync(CancellationToken ct = default)
    {
        return await _context.AiKnowledgeBases
            .AsNoTracking()
            .OrderByDescending(k => k.LastUpdated)
            .ThenByDescending(k => k.Id)
            .ToListAsync(ct);
    }

    public async Task<AiKnowledgeBase?> GetKnowledgeEntryByIdAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var q = track ? _context.AiKnowledgeBases : _context.AiKnowledgeBases.AsNoTracking();
        return await q.FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    public Task AddKnowledgeEntryAsync(AiKnowledgeBase entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            _context.AiKnowledgeBases.Add(entity);
            return Task.CompletedTask;
        }, ct);
    }

    public Task SaveKnowledgeEntryAsync(AiKnowledgeBase entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public async Task<IReadOnlyList<AiModelConfig>> GetModelConfigsAsync(CancellationToken ct = default)
    {
        return await _context.AiModelConfigs
            .AsNoTracking()
            .OrderByDescending(m => m.IsActive ?? false)
            .ThenByDescending(m => m.Id)
            .ToListAsync(ct);
    }

    public async Task<AiModelConfig?> GetModelConfigByIdAsync(long id, bool track = false, CancellationToken ct = default)
    {
        var q = track ? _context.AiModelConfigs : _context.AiModelConfigs.AsNoTracking();
        return await q.FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public Task AddModelConfigAsync(AiModelConfig entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ =>
        {
            _context.AiModelConfigs.Add(entity);
            return Task.CompletedTask;
        }, ct);
    }

    public Task SaveModelConfigAsync(AiModelConfig entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public Task<AiSmartDispatching?> GetSmartDispatchByIdAsync(long dispatchId, CancellationToken ct = default)
    {
        return _context.AiSmartDispatchings.FirstOrDefaultAsync(d => d.Id == dispatchId, ct);
    }

    public Task SaveSmartDispatchAsync(AiSmartDispatching entity, CancellationToken ct = default)
    {
        return ExecuteInTransactionAsync(_ => Task.CompletedTask, ct);
    }

    public Task<bool> ChatbotSessionExistsAsync(long sessionId, long customerId, CancellationToken ct = default)
    {
        return _context.ChatbotSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.CustomerId == customerId, ct);
    }
}
