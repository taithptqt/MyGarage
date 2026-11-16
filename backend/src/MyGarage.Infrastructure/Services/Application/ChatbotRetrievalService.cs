using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public partial class ChatbotRetrievalService : IChatbotRetrievalService
{
    private static readonly string[] ServicePriceSignals =
    [
        "base price",
        "service price",
        "gia",
        "bang gia",
        "baogia",
        "bao gia",
        "bao nhieu",
        "het bao nhieu",
        "bao tien",
        "quote",
        "quotation",
        "price",
        "cost"
    ];
    private static readonly string[] AllScopeSignals =
    [
        "toan bo",
        "tat ca",
        "all",
        "full list",
        "danh sach tat ca",
        "tong hop"
    ];
    private static readonly string[] WarrantySignals = ["warranty", "bao hanh", "warranty policy"];
    private static readonly string[] BranchSignals = ["branch", "chi nhanh", "address", "dia chi", "hotline", "phone"];
    private static readonly string[] HoursSignals = ["hours", "open", "close", "operating", "gio lam viec", "mo cua"];
    private static readonly string[] MembershipSignals = ["membership", "member", "tier", "benefit", "uu dai", "khach hang than thiet"];
    private static readonly HashSet<string> ServiceHintStopTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "bao", "gia", "dich", "vu", "service", "price", "cost",
        "hanh", "thong", "tin", "ve",
        "sua", "chua", "chung", "he", "thong", "bao", "duong",
        "cho", "toi", "minh", "em", "anh", "chi", "ban",
        "co", "khong", "la", "nhu", "nao", "nhieu", "het", "tien",
        "xin", "hay", "vui", "long", "cau", "lam", "on", "giup",
        "may", "tao", "thang", "con", "nhe", "voi"
    };

    private readonly IGarageContext _context;
    private readonly ILogger<ChatbotRetrievalService> _logger;

    public ChatbotRetrievalService(
        IGarageContext context,
        ILogger<ChatbotRetrievalService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ChatbotRetrievalResult> RetrieveAsync(
        long customerId,
        long? branchId,
        string question,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuestion = Normalize(question);
        var queryType = DetectQueryType(normalizedQuestion);

        var result = queryType switch
        {
            ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE => await ResolveServiceBasePriceAsync(normalizedQuestion, cancellationToken),
            ChatbotKnowledgeQueryType.WARRANTY_POLICY => await ResolveWarrantyPolicyAsync(normalizedQuestion, cancellationToken),
            ChatbotKnowledgeQueryType.BRANCH_ADDRESS_HOTLINE => await ResolveBranchAddressHotlineAsync(normalizedQuestion, branchId, cancellationToken),
            ChatbotKnowledgeQueryType.OPERATING_HOURS => await ResolveOperatingHoursAsync(normalizedQuestion, branchId, cancellationToken),
            ChatbotKnowledgeQueryType.MEMBERSHIP_BENEFITS => await ResolveMembershipBenefitsAsync(normalizedQuestion, cancellationToken),
            _ => new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.UNKNOWN,
                IsGrounded = false,
                MissingReason = "Query type not supported for deterministic retrieval."
            }
        };

        _logger.LogInformation(
            "Chatbot retrieval processed. CustomerId={CustomerId}, QueryType={QueryType}, Grounded={Grounded}, Ambiguous={Ambiguous}, SourceCount={SourceCount}",
            customerId,
            result.QueryType,
            result.IsGrounded,
            result.IsAmbiguous,
            result.Sources.Count);

        return result;
    }

    private async Task<ChatbotRetrievalResult> ResolveServiceBasePriceAsync(string question, CancellationToken cancellationToken)
    {
        var hasSpecificServiceHint = HasSpecificServiceHint(question);
        var categoryCandidates = await MatchServiceCategoriesAsync(question, cancellationToken);
        var explicitAllScope = IsExplicitAllPricingRequest(question);

        if (!hasSpecificServiceHint && explicitAllScope)
        {
            return await BuildAllServicePricingResultAsync(cancellationToken);
        }

        if (!hasSpecificServiceHint && categoryCandidates.Count > 0)
        {
            if (HasAmbiguousTopCategoryCandidates(categoryCandidates))
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

        var candidates = await MatchServicesAsync(question, cancellationToken);
        if (candidates.Count == 0)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE,
                IsGrounded = false,
                MissingReason = "No matching service found."
            };
        }

        if (HasAmbiguousTopCandidates(candidates))
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

    private async Task<ChatbotRetrievalResult> ResolveWarrantyPolicyAsync(string question, CancellationToken cancellationToken)
    {
        var serviceCandidates = await MatchServicesAsync(question, cancellationToken);
        if (serviceCandidates.Count > 0 && !HasAmbiguousTopCandidates(serviceCandidates))
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
                        Snippet = $"{kb.Topic}: {Truncate(kb.Content, 180)}"
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

    private async Task<List<CategoryMatch>> MatchServiceCategoriesAsync(string question, CancellationToken cancellationToken)
    {
        var normalized = Normalize(question);
        var tokens = Tokenize(normalized);
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

        var matches = categories
            .Select(x =>
            {
                var normalizedCode = Normalize(x.Code);
                var normalizedName = Normalize(x.Name);
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

        return matches;
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

    private async Task<ChatbotRetrievalResult> ResolveBranchAddressHotlineAsync(string question, long? branchId, CancellationToken cancellationToken)
    {
        var branchMatch = await ResolveBranchAsync(question, branchId, cancellationToken);
        if (branchMatch.IsAmbiguous)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.BRANCH_ADDRESS_HOTLINE,
                IsGrounded = false,
                IsAmbiguous = true,
                ClarificationPrompt = branchMatch.ClarificationPrompt
            };
        }

        if (branchMatch.Branch is null)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.BRANCH_ADDRESS_HOTLINE,
                IsGrounded = false,
                MissingReason = "Branch not found."
            };
        }

        var address = string.IsNullOrWhiteSpace(branchMatch.Branch.Address) ? "chưa có địa chỉ" : branchMatch.Branch.Address;
        var hotline = string.IsNullOrWhiteSpace(branchMatch.Branch.Hotline) ? "chưa có hotline" : branchMatch.Branch.Hotline;

        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.BRANCH_ADDRESS_HOTLINE,
            IsGrounded = true,
            ResolvedBranchId = branchMatch.Branch.Id,
            ResolvedBranchName = branchMatch.Branch.Name,
            ResolvedBranchHotline = branchMatch.Branch.Hotline,
            Answer = $"Chi nhánh \"{branchMatch.Branch.Name}\" có địa chỉ: {address}. Hotline: {hotline}.",
            Sources =
            [
                new ChatbotAuditSourceSnippet
                {
                    Label = "Branch contact",
                    Snippet = $"{branchMatch.Branch.Name} | Address={branchMatch.Branch.Address ?? "null"} | Hotline={branchMatch.Branch.Hotline ?? "null"}"
                }
            ]
        };
    }

    private async Task<ChatbotRetrievalResult> ResolveOperatingHoursAsync(string question, long? branchId, CancellationToken cancellationToken)
    {
        var branchMatch = await ResolveBranchAsync(question, branchId, cancellationToken);
        if (branchMatch.IsAmbiguous)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.OPERATING_HOURS,
                IsGrounded = false,
                IsAmbiguous = true,
                ClarificationPrompt = branchMatch.ClarificationPrompt
            };
        }

        if (branchMatch.Branch is null)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.OPERATING_HOURS,
                IsGrounded = false,
                MissingReason = "Branch not found."
            };
        }

        var hours = await _context.BranchOperatingHours
            .AsNoTracking()
            .Where(x => x.BranchId == branchMatch.Branch.Id)
            .OrderBy(x => x.DayOfWeek)
            .Select(x => new
            {
                x.DayOfWeek,
                x.OpenTime,
                x.CloseTime,
                x.IsDayOff
            })
            .ToListAsync(cancellationToken);

        if (hours.Count == 0)
        {
            return new ChatbotRetrievalResult
            {
                QueryType = ChatbotKnowledgeQueryType.OPERATING_HOURS,
                IsGrounded = false,
                ResolvedBranchId = branchMatch.Branch.Id,
                ResolvedBranchName = branchMatch.Branch.Name,
                ResolvedBranchHotline = branchMatch.Branch.Hotline,
                MissingReason = "Operating hours unavailable."
            };
        }

        var formatted = string.Join("; ", hours.Select(x =>
            x.IsDayOff == true
                ? $"{ToDayLabel(x.DayOfWeek)}: nghỉ"
                : $"{ToDayLabel(x.DayOfWeek)}: {x.OpenTime:HH\\:mm}-{x.CloseTime:HH\\:mm}"));

        return new ChatbotRetrievalResult
        {
            QueryType = ChatbotKnowledgeQueryType.OPERATING_HOURS,
            IsGrounded = true,
            ResolvedBranchId = branchMatch.Branch.Id,
            ResolvedBranchName = branchMatch.Branch.Name,
            ResolvedBranchHotline = branchMatch.Branch.Hotline,
            Answer = $"Giờ hoạt động của chi nhánh \"{branchMatch.Branch.Name}\": {formatted}.",
            Sources =
            [
                new ChatbotAuditSourceSnippet
                {
                    Label = "Branch operating hours",
                    Snippet = Truncate(formatted, 220)
                }
            ]
        };
    }

    private async Task<ChatbotRetrievalResult> ResolveMembershipBenefitsAsync(string question, CancellationToken cancellationToken)
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
                        Snippet = Truncate(summary, 220)
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
                        Snippet = $"{kb.Topic}: {Truncate(kb.Content, 180)}"
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

    private async Task<List<ServiceMatch>> MatchServicesAsync(string question, CancellationToken cancellationToken)
    {
        var normalized = Normalize(question);
        var tokens = ExtractServiceHintTokens(normalized);
        var explicitCodeHint = ServiceCodeHintRegex().IsMatch(normalized);
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

        var matches = services
            .Select(x =>
            {
                var score = 0;
                var normalizedCode = Normalize(x.Code);
                var normalizedName = Normalize(x.Name);
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

        return matches;
    }

    private async Task<BranchResolution> ResolveBranchAsync(string question, long? branchId, CancellationToken cancellationToken)
    {
        if (branchId.HasValue)
        {
            var branch = await _context.Branches
                .AsNoTracking()
                .Where(x => x.Id == branchId.Value && x.IsDeleted != true)
                .Select(x => new BranchSummary
                {
                    Id = x.Id,
                    Name = x.Name,
                    Address = x.Address,
                    Hotline = x.Hotline
                })
                .FirstOrDefaultAsync(cancellationToken);
            return new BranchResolution { Branch = branch };
        }

        var branchNameHint = ExtractBranchHint(question);
        if (string.IsNullOrWhiteSpace(branchNameHint))
        {
            var activeBranches = await _context.Branches
                .AsNoTracking()
                .Where(x => x.IsDeleted != true && (x.Status == null || x.Status == "ACTIVE"))
                .OrderBy(x => x.Name)
                .Select(x => new BranchSummary
                {
                    Id = x.Id,
                    Name = x.Name,
                    Address = x.Address,
                    Hotline = x.Hotline
                })
                .Take(5)
                .ToListAsync(cancellationToken);

            if (activeBranches.Count == 1)
            {
                return new BranchResolution { Branch = activeBranches[0] };
            }

            if (activeBranches.Count > 1)
            {
                var optionText = string.Join(", ", activeBranches.Take(3).Select(x => x.Name));
                return new BranchResolution
                {
                    IsAmbiguous = true,
                    ClarificationPrompt = $"Mình cần biết bạn muốn chi nhánh nào. Gợi ý: {optionText}."
                };
            }

            return new BranchResolution();
        }

        var branchTokenHints = Tokenize(branchNameHint)
            .Where(x => x is not "hotline" and not "phone" and not "address" and not "dia" and not "chi")
            .ToList();
        if (branchTokenHints.Count == 0)
        {
            branchTokenHints.Add(branchNameHint);
        }

        var allBranches = await _context.Branches
            .AsNoTracking()
            .Where(x => x.IsDeleted != true)
            .Select(x => new BranchSummary
            {
                Id = x.Id,
                Name = x.Name,
                Address = x.Address,
                Hotline = x.Hotline
            })
            .ToListAsync(cancellationToken);

        var candidates = allBranches
            .Where(x =>
            {
                var normalizedName = Normalize(x.Name);
                return branchTokenHints.All(t => normalizedName.Contains(t, StringComparison.OrdinalIgnoreCase));
            })
            .OrderBy(x => x.Name)
            .ToList();

        if (candidates.Count == 0)
        {
            return new BranchResolution();
        }

        if (candidates.Count > 1)
        {
            var optionText = string.Join(", ", candidates.Take(3).Select(x => x.Name));
            return new BranchResolution
            {
                IsAmbiguous = true,
                ClarificationPrompt = $"Mình thấy nhiều chi nhánh phù hợp: {optionText}. Bạn muốn chi nhánh nào?"
            };
        }

        return new BranchResolution { Branch = candidates[0] };
    }

    private async Task<AiKnowledgeBase?> MatchKnowledgeBaseAsync(string[] keywords, CancellationToken cancellationToken)
    {
        var keywordSet = keywords.Select(Normalize).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (keywordSet.Count == 0)
        {
            return null;
        }

        var records = await _context.AiKnowledgeBases
            .AsNoTracking()
            // Only explicitly published customer/public documents may leave the server.
            // Unclassified, staff and admin knowledge is denied before materialization.
            .Where(x => x.Topic.StartsWith("PUBLIC:") || x.Topic.StartsWith("CUSTOMER:"))
            .Select(x => new
            {
                Entity = x,
                Topic = x.Topic.ToLower(),
                Content = x.Content.ToLower()
            })
            .ToListAsync(cancellationToken);

        var best = records
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

        return best;
    }

    private static ChatbotKnowledgeQueryType DetectQueryType(string question)
    {
        var hasWarrantySignal = ContainsAny(question, WarrantySignals);
        if (hasWarrantySignal && !HasSpecificServiceHint(question))
        {
            return ChatbotKnowledgeQueryType.WARRANTY_POLICY;
        }

        if (ContainsAny(question, ServicePriceSignals))
        {
            return ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE;
        }

        if (ContainsAny(question, WarrantySignals))
        {
            return ChatbotKnowledgeQueryType.WARRANTY_POLICY;
        }

        if (ContainsAny(question, HoursSignals))
        {
            return ChatbotKnowledgeQueryType.OPERATING_HOURS;
        }

        if (ContainsAny(question, BranchSignals))
        {
            return ChatbotKnowledgeQueryType.BRANCH_ADDRESS_HOTLINE;
        }

        if (ContainsAny(question, MembershipSignals))
        {
            return ChatbotKnowledgeQueryType.MEMBERSHIP_BENEFITS;
        }

        return ChatbotKnowledgeQueryType.UNKNOWN;
    }

    private static bool ContainsAny(string input, IEnumerable<string> keywords)
    {
        return keywords.Any(k => input.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasAmbiguousTopCandidates(IReadOnlyList<ServiceMatch> candidates)
    {
        return candidates.Count > 1 && (candidates[0].Score - candidates[1].Score) <= 1;
    }

    private static bool HasAmbiguousTopCategoryCandidates(IReadOnlyList<CategoryMatch> candidates)
    {
        return candidates.Count > 1 && (candidates[0].Score - candidates[1].Score) <= 1;
    }

    private static bool IsExplicitAllPricingRequest(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var normalized = Normalize(question);
        return ContainsAny(normalized, ServicePriceSignals) && ContainsAny(normalized, AllScopeSignals);
    }

    private static bool HasSpecificServiceHint(string question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return false;
        }

        var normalized = Normalize(question);
        if (ServiceCodeHintRegex().IsMatch(normalized) || ServiceLevelHintRegex().IsMatch(normalized))
        {
            return true;
        }

        var tokens = ExtractServiceHintTokens(normalized);
        if (tokens.Count >= 2)
        {
            return true;
        }

        return tokens.Count == 1 && tokens[0].Length >= 7;
    }

    private static string Normalize(string? input)
    {
        var text = (input ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace('\u0111', 'd')
            .Replace('\u0110', 'd');
    }

    private static List<string> Tokenize(string input)
    {
        return WordTokenRegex()
            .Matches(input)
            .Select(x => x.Value)
            .Where(x => x.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractServiceHintTokens(string input)
    {
        return Tokenize(input)
            .Where(x => !ServiceHintStopTokens.Contains(x))
            .ToList();
    }

    private static string? ExtractBranchHint(string question)
    {
        var match = BranchHintRegex().Match(question);
        if (!match.Success)
        {
            return null;
        }

        var value = Normalize(match.Groups["branch"].Value);
        value = value
            .Replace("hotline", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("phone", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("address", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("dia chi", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        return value;
    }

    private static string ToDayLabel(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            0 => "CN",
            1 => "T2",
            2 => "T3",
            3 => "T4",
            4 => "T5",
            5 => "T6",
            6 => "T7",
            _ => $"Day{dayOfWeek}"
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    [GeneratedRegex(@"\b[a-zA-Z0-9\u00C0-\u024F]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex WordTokenRegex();

    [GeneratedRegex(@"(?:chi nhanh|branch)\s+(?<branch>[a-zA-Z0-9\u00C0-\u024F\s\-]{2,40})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BranchHintRegex();

    [GeneratedRegex(@"\b[a-z]{1,5}\d{2,8}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ServiceCodeHintRegex();

    [GeneratedRegex(@"\bcap\s*\d+\b|\b\d+\s*(?:k|km)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ServiceLevelHintRegex();

    private sealed class ServiceMatch
    {
        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public decimal BasePrice { get; set; }

        public int? WarrantyMonths { get; set; }

        public int? WarrantyKm { get; set; }

        public int Score { get; set; }
    }

    private sealed class BranchResolution
    {
        public bool IsAmbiguous { get; set; }

        public string? ClarificationPrompt { get; set; }

        public BranchSummary? Branch { get; set; }
    }

    private sealed class BranchSummary
    {
        public long Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Address { get; set; }

        public string? Hotline { get; set; }
    }

    private sealed class CategoryMatch
    {
        public long Id { get; set; }

        public string Code { get; set; } = null!;

        public string Name { get; set; } = null!;

        public int Score { get; set; }
    }
}

