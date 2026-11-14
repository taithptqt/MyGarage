namespace MyGarage.Application.Ai;

// Metadata only: content and source URLs stay on the existing admin detail API.
public sealed class AiKnowledgeCatalogRow
{
    public long Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public bool? IsDynamic { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public interface IAiKnowledgeCatalogReader
{
    IQueryable<AiKnowledgeCatalogRow> Query();
}

public sealed class AiKnowledgeCatalogQuery(IAiKnowledgeCatalogReader reader)
{
    public IQueryable<AiKnowledgeCatalogRow> Execute() => reader.Query();
}
