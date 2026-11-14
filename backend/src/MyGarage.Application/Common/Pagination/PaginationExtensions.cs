using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyGarage.Application.Common.Pagination;
using MyGarage.Interfaces;

namespace MyGarage.Extensions
{
    public static class PaginationExtensions
    {
        public static async Task<IReadOnlyList<T>> ApplyPaginationAsync<T>(
            this IGenericService<T> service, int? page, int? pageSize, HttpResponse? response) where T : class
        {
            var (normalizedPage, normalizedSize) = NormalizePagination(page, pageSize);
            var (items, total) = await service.GetPageByIdAsync(normalizedPage, normalizedSize);
            response.AppendPaginationHeaders(total, normalizedPage, normalizedSize);
            return items;
        }
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

        public static void AppendPaginationHeaders(
            this HttpResponse? response,
            int totalCount,
            int page,
            int pageSize)
        {
            if (response == null)
            {
                return;
            }

            var totalPages = pageSize <= 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

            response.Headers[HeaderTotalCount] = totalCount.ToString();
            response.Headers[HeaderPage] = page.ToString();
            response.Headers[HeaderPageSize] = pageSize.ToString();
            response.Headers[HeaderTotalPages] = totalPages.ToString();
        }

        public static void AppendPaginationHeaders(
            this HttpContext? httpContext,
            int totalCount,
            int page,
            int pageSize)
        {
            httpContext?.Response?.AppendPaginationHeaders(totalCount, page, pageSize);
        }

        public static PagedEnvelope<T> CreatePagedEnvelope<T>(
            IEnumerable<T>? items,
            int totalCount,
            int page,
            int pageSize,
            HttpResponse? response = null)
        {
            _ = response;
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

        public static async Task<PagedEnvelope<T>> ToPagedEnvelopeAsync<T>(
            this IQueryable<T>? query,
            int? page,
            int? pageSize,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
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
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
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
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
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

        public static List<T> ApplyPagination<T>(
            this IEnumerable<T>? source,
            int? page,
            int? pageSize,
            HttpResponse? response,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize)
        {
            if (source == null)
            {
                response?.AppendPaginationHeaders(0, DefaultPage, defaultPageSize);
                return new List<T>();
            }

            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            var list = source as IReadOnlyList<T> ?? source.ToList();
            var total = list.Count;

            response?.AppendPaginationHeaders(total, normalizedPage, normalizedPageSize);

            var offset = ((long)normalizedPage - 1) * normalizedPageSize;
            return list
                .Skip(offset > int.MaxValue ? int.MaxValue : (int)offset)
                .Take(normalizedPageSize)
                .ToList();
        }

        public static List<T> ApplyPagination<T>(
            this IEnumerable<T>? source,
            HttpResponse? response,
            int? page = null,
            int? pageSize = null,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize)
        {
            return ApplyPagination(source, page, pageSize, response, defaultPageSize, maxPageSize);
        }

        public static List<T> ApplyPagination<T>(
            this IEnumerable<T>? source,
            ControllerBase? controller,
            int? page = null,
            int? pageSize = null,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize)
        {
            return ApplyPagination(source, page, pageSize, controller?.Response, defaultPageSize, maxPageSize);
        }

        public static async Task<List<T>> ApplyPaginationAsync<T>(
            this IQueryable<T>? query,
            int? page,
            int? pageSize,
            HttpResponse? response,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            if (query == null)
            {
                response?.AppendPaginationHeaders(0, DefaultPage, defaultPageSize);
                return new List<T>();
            }

            var (normalizedPage, normalizedPageSize) = NormalizePagination(page, pageSize, defaultPageSize, maxPageSize);
            var total = await query.CountAsync(cancellationToken);

            response?.AppendPaginationHeaders(total, normalizedPage, normalizedPageSize);

            var offset = ((long)normalizedPage - 1) * normalizedPageSize;
            if (offset >= total) return new List<T>();
            return await query
                .Skip((int)offset)
                .Take(normalizedPageSize)
                .ToListAsync(cancellationToken);
        }

        public static Task<List<T>> ApplyPaginationAsync<T>(
            this IQueryable<T>? query,
            HttpResponse? response,
            int? page = null,
            int? pageSize = null,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            return ApplyPaginationAsync(query, page, pageSize, response, defaultPageSize, maxPageSize, cancellationToken);
        }

        public static Task<List<T>> ApplyPaginationAsync<T>(
            this IQueryable<T>? query,
            ControllerBase? controller,
            int? page = null,
            int? pageSize = null,
            int defaultPageSize = DefaultPageSize,
            int maxPageSize = DefaultMaxPageSize,
            CancellationToken cancellationToken = default)
        {
            return ApplyPaginationAsync(query, page, pageSize, controller?.Response, defaultPageSize, maxPageSize, cancellationToken);
        }
    }
}
