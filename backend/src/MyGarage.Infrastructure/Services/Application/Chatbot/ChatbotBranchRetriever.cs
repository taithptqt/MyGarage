using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

internal sealed class ChatbotBranchRetriever
{
    private readonly IGarageContext _context;

    public ChatbotBranchRetriever(IGarageContext context)
    {
        _context = context;
    }

    public async Task<ChatbotRetrievalResult> ResolveBranchAddressHotlineAsync(string question, long? branchId, CancellationToken cancellationToken)
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

    public async Task<ChatbotRetrievalResult> ResolveOperatingHoursAsync(string question, long? branchId, CancellationToken cancellationToken)
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
                ? $"{ChatbotRetrievalTextHelper.ToDayLabel(x.DayOfWeek)}: nghỉ"
                : $"{ChatbotRetrievalTextHelper.ToDayLabel(x.DayOfWeek)}: {x.OpenTime:HH\\:mm}-{x.CloseTime:HH\\:mm}"));

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
                    Snippet = ChatbotRetrievalTextHelper.Truncate(formatted, 220)
                }
            ]
        };
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

        var branchNameHint = ChatbotRetrievalTextHelper.ExtractBranchHint(question);
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

        var branchTokenHints = ChatbotRetrievalTextHelper.Tokenize(branchNameHint)
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
                var normalizedName = ChatbotRetrievalTextHelper.Normalize(x.Name);
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
}
