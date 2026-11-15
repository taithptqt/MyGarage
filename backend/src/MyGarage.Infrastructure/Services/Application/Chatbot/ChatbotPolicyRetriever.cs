using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

internal sealed class ChatbotPolicyRetriever
{
    private readonly IGarageContext _context;
    private readonly ChatbotServiceCatalogMatcher _catalogMatcher;

    public ChatbotPolicyRetriever(
        IGarageContext context,
        ChatbotServiceCatalogMatcher catalogMatcher)
    {
        _context = context;
        _catalogMatcher = catalogMatcher;
    }

    public async Task<ChatbotRetrievalResult> ResolveWarrantyPolicyAsync(string question, CancellationToken cancellationToken)
    {
        var serviceCandidates = await _catalogMatcher.MatchServicesAsync(question, cancellationToken);
        if (serviceCandidates.Count > 0 && !ChatbotServiceCatalogMatcher.HasAmbiguousTopCandidates(serviceCandidates))
        {
            var top = serviceCandidates[0];
            if (top.WarrantyMonths.HasValue || top.WarrantyKm.HasValue)
            {
                var monthsText = top.WarrantyMonths.HasValue ? $"{top.WarrantyMonths.Value} tháng" : "không xác định tháng";
                var kmText = top.WarrantyKm.HasValue ? $"{top.WarrantyKm.Value} km" : "không xác định km";

                return new ChatbotRetrievalResult
                {
                    QueryType = ChatbotKnowledgeQueryType.WARRANTY_POLICY,
                    IsGrounded = true,
                    Answer = $"Chính sách bảo hành cho \"{top.Name}\": {monthsText}, {kmText}.",
                    Sources =
                    [
                        new ChatbotAuditSourceSnippet
                        {
                            Label = "Service warranty",
                            Snippet = $"{top.Code} | {top.Name} | WarrantyMonths={top.WarrantyMonths?.ToString() ?? "null"} | WarrantyKm={top.WarrantyKm?.ToString() ?? "null"}"
                        }
                    ]
                };
            }
        }

        var kb = await MatchKnowledgeBaseAsync(["warranty", "bao hanh", "policy"], cancellationToken);
        if (kb is not null)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.WARRANTY_POLICY,
                IsGrounded = true,
                Answer = kb.Content,
                Sources =
                [
                    new ChatbotAuditSourceSnippet
                    {
                        Label = "Warranty knowledge",
                        Snippet = $"{kb.Topic}: {ChatbotRetrievalTextHelper.Truncate(kb.Content, 180)}"
                    }
                ]
            };
        }

        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.WARRANTY_POLICY,
            IsGrounded = false,
            IsAmbiguous = true,
            ClarificationPrompt = "Bạn muốn kiểm tra bảo hành cho dịch vụ/sản phẩm nào hoặc đơn dịch vụ nào? Mình sẽ tra đúng dữ liệu trong hệ thống."
        };
    }

    public async Task<ChatbotRetrievalResult> ResolveMembershipBenefitsAsync(string question, CancellationToken cancellationToken)
    {
        var tiers = await _context.CustomerTiers
            .AsNoTracking()
            .Where(x => x.IsDeleted != true)
            .OrderBy(x => x.MinSpending)
            .Select(x => new
            {
                x.TierName,
                x.MinSpending,
                x.DiscountPercent,
                x.Benefits
            })
            .ToListAsync(cancellationToken);

        if (tiers.Count > 0)
        {
            var summary = string.Join("; ", tiers.Select(x =>
                $"{x.TierName}: giảm {x.DiscountPercent:0.##}% từ mức chi tiêu {x.MinSpending:0.##}{(string.IsNullOrWhiteSpace(x.Benefits) ? string.Empty : $" ({x.Benefits})")}"));
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.MEMBERSHIP_BENEFITS,
                IsGrounded = true,
                Answer = $"Quyền lợi thành viên hiện có: {summary}.",
                Sources =
                [
                    new ChatbotAuditSourceSnippet
                    {
                        Label = "Membership tiers",
                        Snippet = ChatbotRetrievalTextHelper.Truncate(summary, 220)
                    }
                ]
            };
        }

        var kb = await MatchKnowledgeBaseAsync(["membership", "tier", "benefit", "uu dai"], cancellationToken);
        if (kb is not null)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.MEMBERSHIP_BENEFITS,
                IsGrounded = true,
                Answer = kb.Content,
                Sources =
                [
                    new ChatbotAuditSourceSnippet
                    {
                        Label = "Membership policy",
                        Snippet = $"{kb.Topic}: {ChatbotRetrievalTextHelper.Truncate(kb.Content, 180)}"
                    }
                ]
            };
        }

        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.MEMBERSHIP_BENEFITS,
            IsGrounded = false,
            MissingReason = "Membership data unavailable."
        };
    }

    private async Task<AiKnowledgeBase?> MatchKnowledgeBaseAsync(string[] keywords, CancellationToken cancellationToken)
    {
        var keywordSet = keywords
            .Select(ChatbotRetrievalTextHelper.Normalize)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        if (keywordSet.Count == 0)
        {
            return null;
        }

        var records = await _context.AiKnowledgeBases
            .AsNoTracking()
            .Select(x => new
            {
                Entity = x,
                Topic = x.Topic.ToLower(),
                Content = x.Content.ToLower()
            })
            .ToListAsync(cancellationToken);

        return records
            .Select(x => new
            {
                x.Entity,
                Score = keywordSet.Count(k =>
                    x.Topic.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                    x.Content.Contains(k, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entity.LastUpdated)
            .Select(x => x.Entity)
            .FirstOrDefault();
    }
}
