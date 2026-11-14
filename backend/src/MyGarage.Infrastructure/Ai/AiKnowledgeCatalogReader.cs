using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Ai;

namespace MyGarage.Infrastructure.Ai;

public sealed class AiKnowledgeCatalogReader(AiKnowledgeReadDbContext db) : IAiKnowledgeCatalogReader
{
    public IQueryable<AiKnowledgeCatalogRow> Query() => db.Knowledge.AsNoTracking()
        .Select(x => new AiKnowledgeCatalogRow { Id = x.Id, Topic = x.Topic, IsDynamic = x.IsDynamic, LastUpdated = x.LastUpdated });
}
