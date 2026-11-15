using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Stateless NLP and catalog matching service for resolving service categories and services from customer utterances.
/// </summary>
public sealed class ChatbotServiceMatchingService : IChatbotServiceMatchingService
{
    public ChatbotBookingServiceCategoryOptionDto? PickServiceCategory(
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> options,
        string message,
        bool allowNumericSelection = true)
    {
        if (options.Count == 0 || string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        if (allowNumericSelection)
        {
            var numbers = ChatbotBookingNlpHelper.ExtractNumbers(message).ToList();
            var byIndex = ChatbotBookingNlpHelper.PickByIndex(options, numbers);
            if (byIndex is not null)
            {
                return byIndex;
            }

            var byId = options.FirstOrDefault(option => numbers.Contains(option.CategoryId));
            if (byId is not null)
            {
                return byId;
            }
        }

        var normalizedInput = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return options.FirstOrDefault(option =>
            ChatbotBookingNlpHelper.HasTextualOverlap(normalizedInput, ChatbotBookingNlpHelper.NormalizeNlp(option.CategoryName)));
    }

    public ChatbotBookingServiceCategoryOptionDto? PickServiceCategoryFromBookingUtterance(
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> options,
        string message)
    {
        return PickServiceCategory(
            options,
            message,
            allowNumericSelection: HasServiceCategoryOrdinalSignal(message));
    }

    public string FormatServiceCategoryOptions(IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> options)
    {
        if (options.Count == 0)
        {
            return "- Không có nhóm dịch vụ khả dụng.";
        }

        var lines = options
            .Select((option, index) => $"{index + 1}. [{option.CategoryId}] {option.CategoryName}")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    public string FormatServiceOptions(IReadOnlyList<ChatbotBookingServiceOptionRow> options)
    {
        if (options.Count == 0)
        {
            return "- Không có dịch vụ khả dụng.";
        }

        var lines = options
            .Select((option, index) =>
                $"{index + 1}. [{option.ServiceId}] {option.ServiceName} ({option.ServiceCode}) - {ChatbotBookingNlpHelper.FormatCurrency(option.BasePrice)} - {option.StandardMinutes} phút")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    public List<long> PickServices(
        IReadOnlyList<ChatbotBookingServiceOptionDto> options,
        string message,
        IReadOnlyList<long> current,
        bool allowNumericSelection = true)
    {
        var set = new HashSet<long>(current);

        if (allowNumericSelection)
        {
            var numbers = ChatbotBookingNlpHelper.ExtractNumbers(message).ToList();

            foreach (var number in numbers)
            {
                var matchedById = options.FirstOrDefault(option => option.ServiceId == number);
                if (matchedById is not null)
                {
                    set.Add(matchedById.ServiceId);
                }
            }

            foreach (var number in numbers)
            {
                if (number >= 1 && number <= options.Count)
                {
                    set.Add(options[(int)number - 1].ServiceId);
                }
            }
        }

        var normalizedInput = ChatbotBookingNlpHelper.NormalizeNlp(message);
        var textualMatches = new HashSet<long>();
        var scoredMatches = new List<(long ServiceId, int Score)>();
        foreach (var option in options)
        {
            if (!string.IsNullOrWhiteSpace(option.ServiceCode))
            {
                var normalizedCode = ChatbotBookingNlpHelper.NormalizeNlp(option.ServiceCode);
                if (!string.IsNullOrWhiteSpace(normalizedCode)
                    && normalizedInput.Contains(normalizedCode, StringComparison.Ordinal))
                {
                    set.Add(option.ServiceId);
                    continue;
                }
            }

            var normalizedName = ChatbotBookingNlpHelper.NormalizeNlp(option.ServiceName);
            if (!string.IsNullOrWhiteSpace(normalizedName)
                && normalizedInput.Contains(normalizedName, StringComparison.Ordinal))
            {
                set.Add(option.ServiceId);
                continue;
            }

            if (ChatbotBookingNlpHelper.HasTextualOverlap(normalizedInput, normalizedName))
            {
                textualMatches.Add(option.ServiceId);
            }

            var overlapScore = ChatbotBookingNlpHelper.ScoreTextOverlap(normalizedInput, normalizedName);
            if (overlapScore >= 2)
            {
                scoredMatches.Add((option.ServiceId, overlapScore));
            }
        }

        if (textualMatches.Count == 1)
        {
            set.Add(textualMatches.First());
        }
        else if (textualMatches.Count == 0 && scoredMatches.Count > 0)
        {
            var bestScore = scoredMatches.Max(item => item.Score);
            var bestMatches = scoredMatches
                .Where(item => item.Score == bestScore)
                .Select(item => item.ServiceId)
                .Distinct()
                .ToList();
            if (bestMatches.Count == 1)
            {
                set.Add(bestMatches[0]);
            }
        }

        return set.ToList();
    }

    public ChatbotExtractedServiceSelection? TryExtractServiceSelectionFromBookingUtterance(
        IReadOnlyList<ChatbotBookingServiceCatalogRow> options,
        string? message)
    {
        if (options.Count == 0 || string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var normalizedInput = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return null;
        }

        var explicitMatches = options
            .Where(option =>
            {
                var normalizedCode = ChatbotBookingNlpHelper.NormalizeNlp(option.ServiceCode);
                var normalizedName = ChatbotBookingNlpHelper.NormalizeNlp(option.ServiceName);
                return (!string.IsNullOrWhiteSpace(normalizedCode) && normalizedInput.Contains(normalizedCode, StringComparison.Ordinal))
                    || (!string.IsNullOrWhiteSpace(normalizedName) && normalizedInput.Contains(normalizedName, StringComparison.Ordinal));
            })
            .ToList();

        if (explicitMatches.Count > 0)
        {
            var primary = explicitMatches[0];
            return new ChatbotExtractedServiceSelection
            {
                CategoryId = primary.CategoryId,
                ServiceIds = explicitMatches
                    .Where(option => option.CategoryId == primary.CategoryId)
                    .Select(option => option.ServiceId)
                    .Distinct()
                    .ToList()
            };
        }

        var ranked = options
            .Select(option =>
            {
                var normalizedName = ChatbotBookingNlpHelper.NormalizeNlp(option.ServiceName);
                var normalizedCategoryCode = ChatbotBookingNlpHelper.NormalizeNlp(option.CategoryCode);
                var normalizedCategory = ChatbotBookingNlpHelper.NormalizeNlp(option.CategoryName);
                var normalizedDescription = ChatbotBookingNlpHelper.NormalizeNlp(option.Description);
                var categoryKeywordScore = ScoreCategoryKeywordSignal(normalizedInput, option.CategoryCode, normalizedCategory);
                var nameScore = ChatbotBookingNlpHelper.ScoreTextOverlap(normalizedInput, normalizedName);
                var categoryScore = ChatbotBookingNlpHelper.ScoreTextOverlap(normalizedInput, normalizedCategory);
                var descriptionScore = ChatbotBookingNlpHelper.ScoreTextOverlap(normalizedInput, normalizedDescription);
                var codeScore = string.IsNullOrWhiteSpace(normalizedCategoryCode)
                    ? 0
                    : ChatbotBookingNlpHelper.ScoreTextOverlap(normalizedInput, normalizedCategoryCode);
                var score = (nameScore * 3) + (categoryScore * 2) + (codeScore * 2) + Math.Min(descriptionScore, 2) + categoryKeywordScore;
                if (ChatbotBookingNlpHelper.HasTextualOverlap(normalizedInput, normalizedName))
                {
                    score += 3;
                }

                if (ChatbotBookingNlpHelper.HasTextualOverlap(normalizedInput, normalizedCategory))
                {
                    score += 2;
                }

                return new
                {
                    option.ServiceId,
                    option.CategoryId,
                    Score = score
                };
            })
            .Where(item => item.Score >= 4)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.ServiceId)
            .ToList();

        if (ranked.Count == 0)
        {
            return null;
        }

        var bestScore = ranked[0].Score;
        var bestMatches = ranked
            .Where(item => item.Score == bestScore)
            .ToList();
        if (bestMatches.Count != 1)
        {
            return null;
        }

        return new ChatbotExtractedServiceSelection
        {
            CategoryId = bestMatches[0].CategoryId,
            ServiceIds = [bestMatches[0].ServiceId]
        };
    }

    public bool MentionsSpecificService(string? message, IReadOnlyList<ChatbotBookingServiceOptionDto> options)
    {
        if (string.IsNullOrWhiteSpace(message) || options.Count == 0)
        {
            return false;
        }

        var normalizedInput = ChatbotBookingNlpHelper.NormalizeNlp(message);
        foreach (var option in options)
        {
            var normalizedCode = ChatbotBookingNlpHelper.NormalizeNlp(option.ServiceCode);
            if (!string.IsNullOrWhiteSpace(normalizedCode)
                && normalizedInput.Contains(normalizedCode, StringComparison.Ordinal))
            {
                return true;
            }

            var normalizedName = ChatbotBookingNlpHelper.NormalizeNlp(option.ServiceName);
            if (!string.IsNullOrWhiteSpace(normalizedName)
                && normalizedInput.Contains(normalizedName, StringComparison.Ordinal))
            {
                return true;
            }

            if (ChatbotBookingNlpHelper.HasTextualOverlap(normalizedInput, normalizedName))
            {
                return true;
            }

            if (ChatbotBookingNlpHelper.ScoreTextOverlap(normalizedInput, normalizedName) >= 2)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasServiceCategoryOrdinalSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return normalized.Contains("nhom", StringComparison.Ordinal)
            || normalized.Contains("category", StringComparison.Ordinal)
            || normalized.Contains("loai dich vu", StringComparison.Ordinal);
    }

    public bool HasServiceCategorySelectionSignal(
        string? message,
        IReadOnlyList<ChatbotBookingServiceCategoryOptionDto> categories)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (HasServiceCategoryOrdinalSignal(message))
        {
            return true;
        }

        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return categories.Any(category =>
        {
            var normalizedName = ChatbotBookingNlpHelper.NormalizeNlp(category.CategoryName);
            return !string.IsNullOrWhiteSpace(normalizedName)
                && normalized.Contains(normalizedName, StringComparison.Ordinal);
        });
    }

    public bool HasServiceOrdinalSelectionSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return normalized.Contains("dich vu", StringComparison.Ordinal)
            || normalized.Contains("service", StringComparison.Ordinal)
            || normalized.Contains("svc", StringComparison.Ordinal);
    }

    private static int ScoreCategoryKeywordSignal(string normalizedInput, string? categoryCode, string? normalizedCategoryName)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return 0;
        }

        var normalizedCode = ChatbotBookingNlpHelper.NormalizeNlp(categoryCode);
        var normalizedCategory = ChatbotBookingNlpHelper.NormalizeNlp(normalizedCategoryName);

        return normalizedCode switch
        {
            "insurance repair" => CountKeywordHits(normalizedInput, "bao hiem", "tai nan", "phuc hoi", "boi thuong", "than vo", "dong son") * 3,
            "body paint" => CountKeywordHits(normalizedInput, "dong son", "go son", "son", "vo xe", "than vo", "tai nan") * 3,
            "general repair" => CountKeywordHits(normalizedInput, "sua chua", "chay dau", "dong co", "hop so", "gam", "phuc hoi") * 2,
            "rework" => CountKeywordHits(normalizedInput, "lam lai", "rework", "sua lai", "khac phuc lai") * 3,
            "warranty" or "warrantly" => CountKeywordHits(normalizedInput, "bao hanh", "phu tung bao hanh", "thay bao hanh") * 3,
            "maintenance" or "quick service" => CountKeywordHits(normalizedInput, "bao duong", "cap", "thay dau", "bao tri", "dinh ky") * 3,
            "detailing" or "car care" => CountKeywordHits(normalizedInput, "cham soc", "detailing", "rua", "ve sinh", "phu ceramic", "danh bong") * 3,
            _ when normalizedCategory.Contains("bao hiem", StringComparison.Ordinal) => CountKeywordHits(normalizedInput, "bao hiem", "tai nan", "phuc hoi", "dong son") * 3,
            _ when normalizedCategory.Contains("bao hanh", StringComparison.Ordinal) => CountKeywordHits(normalizedInput, "bao hanh", "phu tung bao hanh") * 3,
            _ when normalizedCategory.Contains("bao duong", StringComparison.Ordinal) => CountKeywordHits(normalizedInput, "bao duong", "dinh ky", "thay dau") * 3,
            _ when normalizedCategory.Contains("sua chua", StringComparison.Ordinal) => CountKeywordHits(normalizedInput, "sua chua", "chay dau", "dong co", "hop so", "gam") * 2,
            _ => 0
        };
    }

    private static int CountKeywordHits(string normalizedInput, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(normalizedInput) || keywords.Length == 0)
        {
            return 0;
        }

        var hits = 0;
        foreach (var keyword in keywords)
        {
            var normalizedKeyword = ChatbotBookingNlpHelper.NormalizeNlp(keyword);
            if (!string.IsNullOrWhiteSpace(normalizedKeyword)
                && normalizedInput.Contains(normalizedKeyword, StringComparison.Ordinal))
            {
                hits++;
            }
        }

        return hits;
    }
}
