using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public interface IChatbotLlmPersistence
{
    Task<IReadOnlyList<AiModelConfig>> GetActiveModelConfigsAsync(CancellationToken cancellationToken = default);

    Task AddActionLogAsync(ChatbotActionLog log, CancellationToken cancellationToken = default);
}

public sealed class EfChatbotLlmPersistence : IChatbotLlmPersistence
{
    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;

    public EfChatbotLlmPersistence(IGarageContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<AiModelConfig>> GetActiveModelConfigsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AiModelConfigs
            .AsNoTracking()
            .Where(x => x.IsActive == true)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddActionLogAsync(ChatbotActionLog log, CancellationToken cancellationToken = default)
    {
        if (log.CreatedAt == default)
        {
            log.CreatedAt = _clock.UtcNow;
        }

        _context.ChatbotActionLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
