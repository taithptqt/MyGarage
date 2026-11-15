using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Ai;
using MyGarage.Data;

namespace MyGarage.Infrastructure.Ai;

public sealed class AiKnowledgePageReader(GarageContext db) : IAiKnowledgePageReader
{
    public async Task<(IReadOnlyList<AiKnowledgeItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.AiKnowledgeBases.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total) return (Array.Empty<AiKnowledgeItem>(), total);
        var items = await query.OrderBy(x => x.Id).Skip((int)offset).Take(pageSize)
            .Select(x => new AiKnowledgeItem(x.Id, x.Topic, x.Content, x.SourceUrl, x.IsDynamic, x.LastUpdated))
            .ToListAsync(cancellationToken);
        return (items, total);
    }
}
