using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;

namespace MyGarage.Services;

internal sealed class ChatbotServiceCatalogMatcher
{
    private readonly IGarageContext _context;

    public ChatbotServiceCatalogMatcher(IGarageContext context)
    {
        _context = context;
    }

    public static bool HasAmbiguousTopCandidates(IReadOnlyList<ServiceMatch> candidates)
    {
        return candidates.Count > 1 && (candidates[0].Score - candidates[1].Score) <= 1;
    }

    public static bool HasAmbiguousTopCategoryCandidates(IReadOnlyList<CategoryMatch> candidates)
    {
        return candidates.Count > 1 && (candidates[0].Score - candidates[1].Score) <= 1;
    }

    public async Task<List<CategoryMatch>> MatchServiceCategoriesAsync(string question, CancellationToken cancellationToken)
    {
        var normalized = ChatbotRetrievalTextHelper.Normalize(question);
        var tokens = ChatbotRetrievalTextHelper.Tokenize(normalized);
        if (tokens.Count == 0)
        {
            return [];
        }

        var categories = await _context.ServiceCategories
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && (x.IsActive == null || x.IsActive == true))
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name
            })
            .ToListAsync(cancellationToken);

        return categories
            .Select(x =>
            {
                var normalizedCode = ChatbotRetrievalTextHelper.Normalize(x.Code);
                var normalizedName = ChatbotRetrievalTextHelper.Normalize(x.Name);
                var score = 0;

                if (!string.IsNullOrWhiteSpace(normalizedName) &&
                    normalized.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                }

                if (!string.IsNullOrWhiteSpace(normalizedCode) &&
                    normalized.Contains(normalizedCode, StringComparison.OrdinalIgnoreCase))
                {
                    score += 8;
                }

                score += tokens.Count(t => normalizedName.Contains(t, StringComparison.OrdinalIgnoreCase)) * 2;

                return new CategoryMatch
                {
                    Id = x.Id,
                    Name = x.Name,
                    Code = x.Code,
                    Score = score
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name)
            .ToList();
    }

    public async Task<List<ServiceMatch>> MatchServicesAsync(string question, CancellationToken cancellationToken)
    {
        var normalized = ChatbotRetrievalTextHelper.Normalize(question);
        var tokens = ChatbotRetrievalTextHelper.ExtractServiceHintTokens(normalized);
        var explicitCodeHint = ChatbotRetrievalTextHelper.HasServiceCodeHint(normalized);
        if (tokens.Count == 0 && !explicitCodeHint)
        {
            return [];
        }

        var services = await _context.StandardServices
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && (x.IsActive == null || x.IsActive == true))
            .Select(x => new
            {
                x.Code,
                x.Name,
                x.BasePrice,
                x.WarrantyMonths,
                x.WarrantyKm
            })
            .ToListAsync(cancellationToken);

        return services
            .Select(x =>
            {
                var score = 0;
                var normalizedCode = ChatbotRetrievalTextHelper.Normalize(x.Code);
                var normalizedName = ChatbotRetrievalTextHelper.Normalize(x.Name);
                if (!string.IsNullOrWhiteSpace(normalizedCode) && normalized.Contains(normalizedCode, StringComparison.OrdinalIgnoreCase))
                {
                    score += 7;
                }

                if (!string.IsNullOrWhiteSpace(normalizedName) && normalized.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 8;
                }

                var tokenMatches = tokens.Count(t => normalizedName.Contains(t, StringComparison.OrdinalIgnoreCase));
                score += tokenMatches * 2;
                if (tokenMatches == 0 &&
                    !string.IsNullOrWhiteSpace(normalizedCode) &&
                    !normalized.Contains(normalizedCode, StringComparison.OrdinalIgnoreCase) &&
                    !normalized.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    score = 0;
                }

                return new ServiceMatch
                {
                    Code = x.Code,
                    Name = x.Name,
                    BasePrice = x.BasePrice,
                    WarrantyMonths = x.WarrantyMonths,
                    WarrantyKm = x.WarrantyKm,
                    Score = score
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Name)
            .ToList();
    }

    internal sealed class ServiceMatch
    {
        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public decimal BasePrice { get; set; }

        public int? WarrantyMonths { get; set; }

        public int? WarrantyKm { get; set; }

        public int Score { get; set; }
    }

    internal sealed class CategoryMatch
    {
        public long Id { get; set; }

        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int Score { get; set; }
    }
}
