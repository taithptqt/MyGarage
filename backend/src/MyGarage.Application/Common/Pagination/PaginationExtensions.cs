using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyGarage.Application.Common.Pagination;
using MyGarage.Interfaces;

namespace MyGarage.Extensions
{
    public static class PaginationExtensions
    {
        public const string HeaderTotalCount = "X-Total-Count";
        public const string HeaderPage = "X-Page";
        public const string HeaderPageSize = "X-Page-Size";
        public const string HeaderTotalPages = "X-Total-Pages";

        public const int DefaultPage = 1;
        public const int DefaultPageSize = 50;
        public const int DefaultMaxPageSize = 100;

        public static (int NormalizedPage, int NormalizedPageSize) NormalizePagination(
            int? page,
            int? pageSize,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize)
        {
            var normalizedPage = Math.Max(1, page.GetValueOrDefault(DefaultPage));
            var rawSize = pageSize.GetValueOrDefault(defaultPageSize);
            if (rawSize <= 0)
            {
                rawSize = defaultPageSize;
            }
            var normalizedPageSize = Math.Clamp(rawSize, 1, maxPageSize);
            return (normalizedPage, normalizedPageSize);
        }

        public static PagedEnvelope<T> CreatePagedEnvelope<T>(
            IEnumerable<T>? items,
            int totalCount,
            int page,
            int pageSize)
        {
            return PagedEnvelope<T>.Create(items, totalCount, page, pageSize);
        }

        public static async Task<PagedEnvelope<T>> ToPagedEnvelopeAsync<T>(
            this IGenericService<T> service,
            int? page,
            int? pageSize,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize) where T : class
        {
            var (normalizedPage, normalizedSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            var (items, total) = await service.GetPageByIdAsync(normalizedPage, normalizedSize);
            return PagedEnvelope<T>.Create(items, total, normalizedPage, normalizedSize);
        }

        public static async Task<PagedEnvelope<TDto>> ToPagedEnvelopeAsync<TSource, TDto>(
            this IGenericService<TSource> service,
            int? page,
            int? pageSize,
            Func<IReadOnlyList<TSource>, IEnumerable<TDto>> map,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize) where TSource : class
        {
            var (normalizedPage, normalizedSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            var (items, total) = await service.GetPageByIdAsync(normalizedPage, normalizedSize);
            return PagedEnvelope<TDto>.Create(map(items), total, normalizedPage, normalizedSize);
        }

        public static PagedEnvelope<T> ToPagedEnvelope<T>(
            this IEnumerable<T>? source,
            int? page,
            int? pageSize,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize)
        {
            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            if (source == null)
            {
                return PagedEnvelope<T>.Create(Array.Empty<T>(), 0, normalizedPage, normalizedPageSize);
            }

            var list = source as IReadOnlyList<T> ?? source.ToList();
            var total = list.Count;
            var offset = ((long)normalizedPage - 1) * normalizedPageSize;
            var paged = list
                .Skip(offset > int.MaxValue ? int.MaxValue : (int)offset)
                .Take(normalizedPageSize)
                .ToList();

            return PagedEnvelope<T>.Create(paged, total, normalizedPage, normalizedPageSize);
        }
    }
}
