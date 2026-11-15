using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyGarage.Application.Common.Pagination;

namespace MyGarage.Filters;

public sealed class PagedEnvelopeHeadersFilter : IResultFilter
{
    private static readonly ConcurrentDictionary<Type, Func<object, IPagedEnvelopeMetadata?>?> MetadataExtractorCache = new();

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: not null } objectResult ||
            !TryExtractMetadata(objectResult.Value, out var metadata))
        {
            return;
        }

        var headers = context.HttpContext.Response.Headers;
        SetHeaderIfAbsent(headers, "X-Total-Count", metadata.TotalCount.ToString(CultureInfo.InvariantCulture));
        SetHeaderIfAbsent(headers, "X-Page", metadata.Page.ToString(CultureInfo.InvariantCulture));
        SetHeaderIfAbsent(headers, "X-Page-Size", metadata.PageSize.ToString(CultureInfo.InvariantCulture));
        SetHeaderIfAbsent(headers, "X-Total-Pages", metadata.TotalPages.ToString(CultureInfo.InvariantCulture));

        if (!headers.ContainsKey("Link") && metadata.TotalPages > 0)
        {
            var linkHeader = BuildLinkHeader(context.HttpContext.Request, metadata);
            if (!string.IsNullOrEmpty(linkHeader))
            {
                headers["Link"] = linkHeader;
            }
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static void SetHeaderIfAbsent(IHeaderDictionary headers, string key, string value)
    {
        if (!headers.ContainsKey(key))
        {
            headers[key] = value;
        }
    }

    private static string BuildLinkHeader(HttpRequest request, IPagedEnvelopeMetadata metadata)
    {
        var path = (request.PathBase + request.Path).ToString();
        if (string.IsNullOrEmpty(path))
        {
            path = "/";
        }

        var preservedParams = new List<KeyValuePair<string, string>>();
        foreach (var kvp in request.Query)
        {
            if (string.Equals(kvp.Key, "page", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kvp.Key, "pageSize", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in kvp.Value)
            {
                if (value is not null)
                {
                    preservedParams.Add(new KeyValuePair<string, string>(kvp.Key, value));
                }
            }
        }

        var pageSize = Math.Max(1, metadata.PageSize);
        var links = new List<string>(4)
        {
            BuildLinkEntry(path, preservedParams, 1, pageSize, "first")
        };

        if (metadata.HasPreviousPage)
        {
            links.Add(BuildLinkEntry(path, preservedParams, Math.Max(1, metadata.Page - 1), pageSize, "prev"));
        }

        if (metadata.HasNextPage)
        {
            links.Add(BuildLinkEntry(path, preservedParams, Math.Min(metadata.TotalPages, metadata.Page + 1), pageSize, "next"));
        }

        links.Add(BuildLinkEntry(path, preservedParams, metadata.TotalPages, pageSize, "last"));

        return string.Join(", ", links);
    }

    private static string BuildLinkEntry(
        string path,
        List<KeyValuePair<string, string>> preservedParams,
        int page,
        int pageSize,
        string rel)
    {
        var qb = new QueryBuilder(preservedParams)
        {
            { "page", page.ToString(CultureInfo.InvariantCulture) },
            { "pageSize", pageSize.ToString(CultureInfo.InvariantCulture) }
        };

        return $"<{path}{qb.ToQueryString()}>; rel=\"{rel}\"";
    }

    private static bool TryExtractMetadata(object value, out IPagedEnvelopeMetadata metadata)
    {
        if (value is IPagedEnvelopeMetadata direct)
        {
            metadata = direct;
            return true;
        }

        var extractor = MetadataExtractorCache.GetOrAdd(value.GetType(), BuildExtractor);
        if (extractor is not null)
        {
            var extracted = extractor(value);
            if (extracted is not null)
            {
                metadata = extracted;
                return true;
            }
        }

        metadata = null!;
        return false;
    }

    private static Func<object, IPagedEnvelopeMetadata?>? BuildExtractor(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var totalCountProp = type.GetProperty("totalCount", flags);
        var pageProp = type.GetProperty("page", flags);
        var pageSizeProp = type.GetProperty("pageSize", flags);
        var totalPagesProp = type.GetProperty("totalPages", flags);
        var hasPreviousProp = type.GetProperty("hasPreviousPage", flags);
        var hasNextProp = type.GetProperty("hasNextPage", flags);

        if (totalCountProp?.PropertyType != typeof(int) ||
            pageProp?.PropertyType != typeof(int) ||
            pageSizeProp?.PropertyType != typeof(int) ||
            totalPagesProp?.PropertyType != typeof(int) ||
            hasPreviousProp?.PropertyType != typeof(bool) ||
            hasNextProp?.PropertyType != typeof(bool))
        {
            return null;
        }

        return obj => new ReflectedPagedMetadata(
            (int)totalCountProp.GetValue(obj)!,
            (int)pageProp.GetValue(obj)!,
            (int)pageSizeProp.GetValue(obj)!,
            (int)totalPagesProp.GetValue(obj)!,
            (bool)hasPreviousProp.GetValue(obj)!,
            (bool)hasNextProp.GetValue(obj)!);
    }

    private sealed record ReflectedPagedMetadata(
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage) : IPagedEnvelopeMetadata;
}
