namespace MyGarage.Application.Ai;

public sealed record AiKnowledgeItem(long Id, string? Topic, string? Content, string? SourceUrl, bool? IsDynamic, DateTime? LastUpdated);
public sealed record AiKnowledgePage(IReadOnlyList<AiKnowledgeItem> Items, int Total, int Page, int PageSize);

public interface IAiKnowledgePageReader
{
    Task<(IReadOnlyList<AiKnowledgeItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class AiKnowledgePageQuery(IAiKnowledgePageReader reader)
{
    public async Task<AiKnowledgePage> ExecuteAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var current = Math.Max(1, page ?? 1);
        var size = pageSize.GetValueOrDefault(50);
        if (size <= 0) size = 50;
        size = Math.Clamp(size, 1, 100);
        var (items, total) = await reader.ReadAsync(current, size, cancellationToken);
        return new AiKnowledgePage(items, total, current, size);
    }
}
