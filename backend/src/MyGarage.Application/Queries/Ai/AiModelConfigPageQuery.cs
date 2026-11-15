namespace MyGarage.Application.Ai;

public sealed record AiModelConfigItem(long Id, string? Provider, bool HasApiKey, string? ModelName,
    int? MaxTokens, decimal? Temperature, bool? IsActive);
public sealed record AiModelConfigPage(IReadOnlyList<AiModelConfigItem> Items, int Total, int Page, int PageSize);

public interface IAiModelConfigPageReader
{
    Task<(IReadOnlyList<AiModelConfigItem> Items, int Total)> ReadAsync(int page, int pageSize, CancellationToken cancellationToken);
}

public sealed class AiModelConfigPageQuery(IAiModelConfigPageReader reader)
{
    public async Task<AiModelConfigPage> ExecuteAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page.GetValueOrDefault(1));
        var size = pageSize.GetValueOrDefault(50);
        if (size <= 0) size = 50;
        size = Math.Clamp(size, 1, 100);
        var (items, total) = await reader.ReadAsync(normalizedPage, size, cancellationToken);
        return new AiModelConfigPage(items, total, normalizedPage, size);
    }
}
