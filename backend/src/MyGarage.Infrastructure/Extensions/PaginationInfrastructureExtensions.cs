using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Common.Pagination;

namespace MyGarage.Extensions
{
    public static class PaginationInfrastructureExtensions
    {
        public static async Task<PagedEnvelope<T>> ToPagedEnvelopeAsync<T>(
            this IQueryable<T>? query,
            int? page,
            int? pageSize,
            int defaultPageSize = PaginationExtensions.DefaultPageSize,
            int maxPageSize = PaginationExtensions.DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            var (normalizedPage, normalizedPageSize) = PaginationExtensions.NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            if (query == null)
            {
                return PagedEnvelope<T>.Create(Array.Empty<T>(), 0, normalizedPage, normalizedPageSize);
            }

            var total = await query.CountAsync(cancellationToken);
            var offset = ((long)normalizedPage - 1) * normalizedPageSize;
            var items = offset >= total
                ? new List<T>()
                : await query
                    .Skip((int)offset)
                    .Take(normalizedPageSize)
                    .ToListAsync(cancellationToken);

            return PagedEnvelope<T>.Create(items, total, normalizedPage, normalizedPageSize);
        }

        public static async Task<PagedEnvelope<TDto>> ToPagedEnvelopeAsync<TSource, TDto>(
            this IQueryable<TSource>? query,
            int? page,
            int? pageSize,
            Func<List<TSource>, IEnumerable<TDto>> map,
            int defaultPageSize = PaginationExtensions.DefaultPageSize,
            int maxPageSize = PaginationExtensions.DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            var (normalizedPage, normalizedPageSize) = PaginationExtensions.NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            if (query == null)
            {
                return PagedEnvelope<TDto>.Create(Array.Empty<TDto>(), 0, normalizedPage, normalizedPageSize);
            }

            var total = await query.CountAsync(cancellationToken);
            var offset = ((long)normalizedPage - 1) * normalizedPageSize;
            var items = offset >= total
                ? new List<TSource>()
                : await query
                    .Skip((int)offset)
                    .Take(normalizedPageSize)
                    .ToListAsync(cancellationToken);

            return PagedEnvelope<TDto>.Create(map(items), total, normalizedPage, normalizedPageSize);
        }

        public static async Task<PagedEnvelope<TDto>> ToPagedEnvelopeAsync<TSource, TDto>(
            this IQueryable<TSource>? query,
            int? page,
            int? pageSize,
            Func<List<TSource>, Task<IEnumerable<TDto>>> mapAsync,
            int defaultPageSize = PaginationExtensions.DefaultPageSize,
            int maxPageSize = PaginationExtensions.DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            var (normalizedPage, normalizedPageSize) = PaginationExtensions.NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            if (query == null)
            {
                return PagedEnvelope<TDto>.Create(Array.Empty<TDto>(), 0, normalizedPage, normalizedPageSize);
            }

            var total = await query.CountAsync(cancellationToken);
            var offset = ((long)normalizedPage - 1) * normalizedPageSize;
            var items = offset >= total
                ? new List<TSource>()
                : await query
                    .Skip((int)offset)
                    .Take(normalizedPageSize)
                    .ToListAsync(cancellationToken);

            var mapped = await mapAsync(items);
            return PagedEnvelope<TDto>.Create(mapped, total, normalizedPage, normalizedPageSize);
        }
    }
}
