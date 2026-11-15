using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGarage.Application.Common.Pagination;

public interface IPagedEnvelopeMetadata
{
    int TotalCount { get; }
    int Page { get; }
    int PageSize { get; }
    int TotalPages { get; }
    bool HasPreviousPage { get; }
    bool HasNextPage { get; }
}

public sealed record PagedEnvelope<T> : IPagedEnvelopeMetadata
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Total => TotalCount;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public static PagedEnvelope<T> Create(IEnumerable<T>? items, int totalCount, int page, int pageSize)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Max(1, pageSize);
        var safeTotal = Math.Max(0, totalCount);
        var totalPages = safeTotal == 0 ? 0 : (int)Math.Ceiling((double)safeTotal / normalizedPageSize);

        return new PagedEnvelope<T>
        {
            Items = items?.ToList() ?? new List<T>(),
            TotalCount = safeTotal,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalPages = totalPages
        };
    }
}
