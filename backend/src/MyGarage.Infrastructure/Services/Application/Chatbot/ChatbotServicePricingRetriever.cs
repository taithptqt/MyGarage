using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

internal sealed class ChatbotServicePricingRetriever
{
    private readonly IGarageContext _context;
    private readonly ChatbotServiceCatalogMatcher _catalogMatcher;

    public ChatbotServicePricingRetriever(
        IGarageContext context,
        ChatbotServiceCatalogMatcher catalogMatcher)
    {
        _context = context;
        _catalogMatcher = catalogMatcher;
    }

    public async Task<ChatbotRetrievalResult> ResolveServiceBasePriceAsync(string question, CancellationToken cancellationToken)
    {
        var hasSpecificServiceHint = ChatbotRetrievalTextHelper.HasSpecificServiceHint(question);
        var categoryCandidates = await _catalogMatcher.MatchServiceCategoriesAsync(question, cancellationToken);
        var explicitAllScope = ChatbotRetrievalTextHelper.IsExplicitAllPricingRequest(question);

        if (!hasSpecificServiceHint && explicitAllScope)
        {
            return await BuildAllServicePricingResultAsync(cancellationToken);
        }

        if (!hasSpecificServiceHint && categoryCandidates.Count > 0)
        {
            if (ChatbotServiceCatalogMatcher.HasAmbiguousTopCategoryCandidates(categoryCandidates))
            {
                var options = string.Join(", ", categoryCandidates.Take(3).Select(x => x.Name));
                return new ChatbotRetrievalResult
                {
                    QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
                    IsGrounded = false,
                    IsAmbiguous = true,
                    ClarificationPrompt = $"Mình thấy nhiều nhóm dịch vụ gần đúng: {options}. Bạn muốn báo giá nhóm nào?"
                };
            }

            return await BuildServiceCategoryPricingResultAsync(
                categoryCandidates[0].Id,
                categoryCandidates[0].Name,
                cancellationToken);
        }

        if (!hasSpecificServiceHint)
        {
            return await BuildCategorySelectionPromptAsync(cancellationToken);
        }

        var candidates = await _catalogMatcher.MatchServicesAsync(question, cancellationToken);
        if (candidates.Count == 0)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
                IsGrounded = false,
                MissingReason = "No matching service found."
            };
        }

        if (ChatbotServiceCatalogMatcher.HasAmbiguousTopCandidates(candidates))
        {
            var options = string.Join(", ", candidates.Take(3).Select(x => x.Name));
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
                IsGrounded = false,
                IsAmbiguous = true,
                ClarificationPrompt = $"Mình tìm thấy nhiều dịch vụ gần đúng: {options}. Bạn muốn dịch vụ nào?"
            };
        }

        var top = candidates[0];
        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
            IsGrounded = true,
            Answer = $"Giá dịch vụ cơ bản cho \"{top.Name}\" hiện là {top.BasePrice:0.##}.",
            Sources =
            [
                new ChatbotAuditSourceSnippet
                {
                    Label = "Service pricing",
                    Snippet = $"{top.Code} | {top.Name} | BasePrice={top.BasePrice:0.##}"
                }
            ]
        };
    }

    private async Task<ChatbotRetrievalResult> BuildServiceCategoryPricingResultAsync(
        long categoryId,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var services = await _context.StandardServices
            .AsNoTracking()
            .Where(x =>
                x.CategoryId == categoryId &&
                x.IsDeleted != true &&
                (x.IsActive == null || x.IsActive == true))
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Code,
                x.Name,
                x.BasePrice
            })
            .ToListAsync(cancellationToken);

        if (services.Count == 0)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
                IsGrounded = false,
                MissingReason = $"No active service found in category '{categoryName}'."
            };
        }

        var minPrice = services.Min(x => x.BasePrice);
        var maxPrice = services.Max(x => x.BasePrice);
        var priceLines = string.Join(
            "\n",
            services.Select(x => $"- {x.Name}: {x.BasePrice:0.##} VND"));

        var sources = new List<ChatbotAuditSourceSnippet>
        {
            new()
            {
                Label = "Service category pricing",
                Snippet = $"{categoryName} | count={services.Count} | min={minPrice:0.##} | max={maxPrice:0.##}"
            }
        };

        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
            IsGrounded = true,
            Answer =
                $"Báo giá toàn bộ dịch vụ trong nhóm \"{categoryName}\" (theo dữ liệu hệ thống):\n" +
                $"{priceLines}\n" +
                $"Tổng số: {services.Count} dịch vụ. Khoảng giá: {minPrice:0.##} - {maxPrice:0.##} VND.",
            Sources = sources
        };
    }

    private async Task<ChatbotRetrievalResult> BuildAllServicePricingResultAsync(
        CancellationToken cancellationToken)
    {
        var services = await _context.StandardServices
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && (x.IsActive == null || x.IsActive == true))
            .OrderBy(x => x.Category.Name)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                CategoryName = x.Category.Name,
                x.Code,
                x.Name,
                x.BasePrice
            })
            .ToListAsync(cancellationToken);

        if (services.Count == 0)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
                IsGrounded = false,
                MissingReason = "No active service found in system."
            };
        }

        var minPrice = services.Min(x => x.BasePrice);
        var maxPrice = services.Max(x => x.BasePrice);
        var lines = string.Join(
            "\n",
            services.Select(x => $"- [{x.CategoryName}] {x.Name}: {x.BasePrice:0.##} VND"));

        var categoryCount = services
            .Select(x => x.CategoryName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
            IsGrounded = true,
            Answer =
                "Báo giá toàn bộ dịch vụ trong hệ thống hiện tại:\n" +
                $"{lines}\n" +
                $"Tổng số: {services.Count} dịch vụ, {categoryCount} nhóm. Khoảng giá: {minPrice:0.##} - {maxPrice:0.##} VND.",
            Sources =
            [
                new ChatbotAuditSourceSnippet
                {
                    Label = "All service pricing",
                    Snippet = $"count={services.Count} | categories={categoryCount} | min={minPrice:0.##} | max={maxPrice:0.##}"
                }
            ]
        };
    }

    private async Task<ChatbotRetrievalResult> BuildCategorySelectionPromptAsync(
        CancellationToken cancellationToken)
    {
        var categories = await _context.ServiceCategories
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && (x.IsActive == null || x.IsActive == true))
            .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .Take(6)
            .ToListAsync(cancellationToken);

        var categoryText = categories.Count > 0
            ? string.Join(", ", categories)
            : "Bảo dưỡng, Sửa chữa chung, Dịch vụ nhanh";

        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
            IsGrounded = false,
            IsAmbiguous = true,
            ClarificationPrompt =
                $"Bạn muốn báo giá theo nhóm nào? Ví dụ: {categoryText}. Nếu muốn toàn bộ hệ thống, hãy nhắn rõ \"báo giá tất cả dịch vụ\"."
        };
    }
}
