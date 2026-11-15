using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Ai;
using MyGarage.Data;

namespace MyGarage.Infrastructure.Ai;

public sealed class AiKnowledgeCatalogReader(GarageContext db) : IAiKnowledgeCatalogReader
{
    public IQueryable<AiKnowledgeCatalogRow> Query() => db.AiKnowledgeBases.AsNoTracking()
        .Select(x => new AiKnowledgeCatalogRow { Id = x.Id, Topic = x.Topic, IsDynamic = x.IsDynamic, LastUpdated = x.LastUpdated });
}
