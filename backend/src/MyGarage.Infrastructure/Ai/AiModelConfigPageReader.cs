using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Ai;

namespace MyGarage.Infrastructure.Ai;

public sealed class AiModelConfigPageReader(AiModelConfigReadDbContext db) : IAiModelConfigPageReader
{
    public async Task<(IReadOnlyList<AiModelConfigItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Configs.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total) return (Array.Empty<AiModelConfigItem>(), total);
        // Compute the flag with the same .NET whitespace semantics as the legacy mapper;
        // never expose the key in a response or include it in logs.
        var rows = await query.OrderBy(x => x.Id).Skip((int)offset).Take(pageSize).ToListAsync(cancellationToken);
        return (rows.Select(x => new AiModelConfigItem(x.Id, x.Provider, !string.IsNullOrWhiteSpace(x.ApiKey),
            x.ModelName, x.MaxTokens, x.Temperature, x.IsActive)).ToList(), total);
    }
}
