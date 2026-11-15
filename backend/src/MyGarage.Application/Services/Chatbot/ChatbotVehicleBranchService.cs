using System.Text.RegularExpressions;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.DTOs.Responses.Chatbot;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

/// <summary>
/// Handles vehicle identification/creation and branch selection/formatting for the chatbot booking workflow.
/// </summary>
public sealed class ChatbotVehicleBranchService : IChatbotVehicleBranchService
{
    private static readonly Regex PlateRegex = new(@"\b[A-Z0-9\-]{5,12}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ModelRegex = new(@"model(?:id)?\s*[:=]?\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex VinRegex = new(@"vin\s*[:=]?\s*([A-Z0-9]{8,20})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] ClosedHints = ["TEMP", "RENOV", "CLOSED", "SUSPEND"];

    private readonly IChatbotBookingStore _store;
    private readonly ISystemClock _clock;

    public ChatbotVehicleBranchService(
        IChatbotBookingStore store,
        ISystemClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<List<ChatbotBookingBranchOptionDto>> LoadAvailableBranchOptionsAsync(CancellationToken ct = default)
    {
        var branches = await _store.GetNonDeletedBranchesAsync(ct);

        var availableBranches = branches
            .Where(IsAvailableBranch)
            .ToList();

        return availableBranches
            .Select((branch, index) => new ChatbotBookingBranchOptionDto
            {
                BranchId = branch.Id,
                BranchName = ResolveBranchDisplayName(branch.Name, index, availableBranches.Count),
                Address = branch.Address,
                Hotline = branch.Hotline
            })
            .ToList();
    }

    public async Task<Vehicle?> TryCreateVehicleAsync(long customerId, string message, CancellationToken ct = default)
    {
        var plate = ParsePlate(message);
        var modelId = await ResolveModelIdFromMessageAsync(message, ct);
        if (string.IsNullOrWhiteSpace(plate) || !modelId.HasValue)
        {
            return null;
        }

        return await _store.CreateOrLinkVehicleWithTransactionAsync(
            customerId,
            plate,
            modelId.Value,
            ParseVin(message),
            ParseManufactureYear(message),
            _clock.UtcNow,
            _clock.LocalToday,
            ct);
    }

    public ChatbotBookingVehicleOptionDto? PickVehicle(
        IReadOnlyList<ChatbotBookingVehicleOptionDto> options,
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
            if (numbers.Count > 0)
            {
                var byIndex = ChatbotBookingNlpHelper.PickByIndex(options, numbers);
                if (byIndex is not null)
                {
                    return byIndex;
                }

                var byId = options.FirstOrDefault(o => numbers.Contains(o.VehicleId));
                if (byId is not null)
                {
                    return byId;
                }
            }
        }

        var normalizedInput = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return null;
        }

        return options.FirstOrDefault(o => ChatbotBookingNlpHelper.ContainsNormalized(o.LicensePlate, normalizedInput))
            ?? options.FirstOrDefault(o => ChatbotBookingNlpHelper.ContainsNormalized(o.VehicleModelName, normalizedInput));
    }

    public ChatbotBookingVehicleOptionDto? PickVehicleFromBookingUtterance(
        IReadOnlyList<ChatbotBookingVehicleOptionDto> options,
        string message)
    {
        return PickVehicle(
            options,
            message,
            allowNumericSelection: HasVehicleSelectionSignal(message));
    }

    public ChatbotBookingBranchOptionDto? PickBranch(
        IReadOnlyList<ChatbotBookingBranchOptionDto> options,
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

            var byId = options.FirstOrDefault(option => numbers.Contains(option.BranchId));
            if (byId is not null)
            {
                return byId;
            }
        }

        var normalizedInput = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return options.FirstOrDefault(option =>
            ChatbotBookingNlpHelper.HasTextualOverlap(normalizedInput, ChatbotBookingNlpHelper.NormalizeNlp(option.BranchName)));
    }

    public ChatbotBookingBranchOptionDto? PickBranchFromBookingUtterance(
        IReadOnlyList<ChatbotBookingBranchOptionDto> options,
        string message)
    {
        return PickBranch(
            options,
            message,
            allowNumericSelection: HasBranchSelectionSignal(message));
    }

    public string FormatVehicleOptions(IReadOnlyList<ChatbotBookingVehicleOptionDto> options)
    {
        if (options.Count == 0)
        {
            return "- Không có xe khả dụng.";
        }

        var lines = options
            .Select((option, index) => $"{index + 1}. [{option.VehicleId}] {option.LicensePlate} - {option.VehicleModelName}")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    public string FormatBranchOptions(IReadOnlyList<ChatbotBookingBranchOptionDto> options)
    {
        if (options.Count == 0)
        {
            return "- Không có chi nhánh khả dụng.";
        }

        var lines = options
            .Select((option, index) =>
                $"{index + 1}. [{option.BranchId}] {option.BranchName} - {option.Address ?? "Chưa cập nhật địa chỉ"}")
            .ToList();
        return string.Join(Environment.NewLine, lines);
    }

    public bool LooksLikeVehicleSelectionReply(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return normalized == "xe"
            || normalized.StartsWith("xe ", StringComparison.Ordinal)
            || normalized == "vehicle"
            || normalized.StartsWith("vehicle ", StringComparison.Ordinal)
            || normalized.Contains("bien so", StringComparison.Ordinal)
            || normalized.Contains("license plate", StringComparison.Ordinal);
    }

    private async Task<long?> ResolveModelIdFromMessageAsync(string message, CancellationToken ct)
    {
        var explicitModelId = ParseModelId(message);
        if (explicitModelId.HasValue)
        {
            var exists = await _store.VehicleModelExistsAsync(explicitModelId.Value, ct);
            if (exists)
            {
                return explicitModelId.Value;
            }
        }

        var normalizedInput = ChatbotBookingNlpHelper.NormalizeNlp(message);
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            return null;
        }

        var candidates = await _store.GetVehicleModelCandidatesAsync(ct);

        if (candidates.Count == 0)
        {
            return null;
        }

        var best = candidates
            .Select(candidate => new
            {
                candidate.Id,
                Score = ChatbotBookingNlpHelper.ScoreTextOverlap(normalizedInput, ChatbotBookingNlpHelper.NormalizeNlp(candidate.Label))
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Id)
            .FirstOrDefault();

        return best is { Score: >= 2 } ? best.Id : null;
    }

    private static string ResolveBranchDisplayName(string? rawName, int index, int totalCount)
    {
        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(rawName);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            if (normalized.Contains("ha noi", StringComparison.Ordinal)
                || normalized.Contains("hanoi", StringComparison.Ordinal)
                || normalized.Contains("bh 01", StringComparison.Ordinal)
                || normalized.Contains("branch 1", StringComparison.Ordinal)
                || normalized.Contains("chi nhanh 1", StringComparison.Ordinal))
            {
                return "Hà Nội";
            }

            if (normalized.Contains("da nang", StringComparison.Ordinal)
                || normalized.Contains("danang", StringComparison.Ordinal)
                || normalized.Contains("bh 02", StringComparison.Ordinal)
                || normalized.Contains("branch 2", StringComparison.Ordinal)
                || normalized.Contains("chi nhanh 2", StringComparison.Ordinal))
            {
                return "Đà Nẵng";
            }

            if (normalized.Contains("tp hcm", StringComparison.Ordinal)
                || normalized.Contains("tphcm", StringComparison.Ordinal)
                || normalized.Contains("ho chi minh", StringComparison.Ordinal)
                || normalized.Contains("sai gon", StringComparison.Ordinal)
                || normalized.Contains("saigon", StringComparison.Ordinal)
                || normalized.Contains("bh 03", StringComparison.Ordinal)
                || normalized.Contains("branch 3", StringComparison.Ordinal)
                || normalized.Contains("chi nhanh 3", StringComparison.Ordinal))
            {
                return "TP HCM";
            }
        }

        if (totalCount == 3)
        {
            return index switch
            {
                0 => "Hà Nội",
                1 => "Đà Nẵng",
                2 => "TP HCM",
                _ => rawName ?? "Chi nhánh"
            };
        }

        return string.IsNullOrWhiteSpace(rawName) ? "Chi nhánh" : rawName.Trim();
    }

    private static bool IsAvailableBranch(ChatbotBookingBranchRow row) =>
        string.Equals(row.Status.Trim(), "ACTIVE", StringComparison.OrdinalIgnoreCase)
        && ClosedHints.All(h => !($"{row.Status} {row.Description} {row.Name}").ToUpperInvariant().Contains(h, StringComparison.Ordinal));

    private static string? ParsePlate(string text)
    {
        var matches = PlateRegex.Matches((text ?? string.Empty).ToUpperInvariant());
        foreach (Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var candidate = match.Value.ToUpperInvariant();
            var hasLetter = candidate.Any(char.IsLetter);
            var hasDigit = candidate.Any(char.IsDigit);
            if (hasLetter && hasDigit)
            {
                return candidate;
            }
        }

        return null;
    }

    private static long? ParseModelId(string text)
    {
        var match = ModelRegex.Match(text ?? string.Empty);
        if (match.Success && long.TryParse(match.Groups[1].Value, out var id) && id > 0)
        {
            return id;
        }

        return null;
    }

    private int? ParseManufactureYear(string text)
    {
        var nowYear = _clock.LocalNow.Year + 1;
        var years = ChatbotBookingNlpHelper.NumberRegex.Matches(text ?? string.Empty)
            .Select(match => int.TryParse(match.Value, out var parsed) ? parsed : 0)
            .Where(year => year >= 1980 && year <= nowYear)
            .ToList();
        return years.Count > 0 ? years[0] : null;
    }

    private static string? ParseVin(string text)
    {
        var m = VinRegex.Match((text ?? string.Empty).ToUpperInvariant());
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    private static bool HasVehicleSelectionSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return Regex.IsMatch(normalized, @"\b(?:xe|vehicle)\s+(?:so\s+)?\d+\b", RegexOptions.IgnoreCase)
            || normalized.Contains("bien so", StringComparison.Ordinal)
            || normalized.Contains("license plate", StringComparison.Ordinal);
    }

    private static bool HasBranchSelectionSignal(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = ChatbotBookingNlpHelper.NormalizeNlp(message);
        return normalized.Contains("chi nhanh", StringComparison.Ordinal)
            || normalized.Contains("branch", StringComparison.Ordinal)
            || normalized.Contains("dia diem", StringComparison.Ordinal)
            || normalized.Contains("garage", StringComparison.Ordinal);
    }
}
