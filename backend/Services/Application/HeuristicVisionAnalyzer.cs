using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class HeuristicVisionAnalyzer : IVisionAnalyzer
{
    public Task<VisionAnalyzerResult> AnalyzeAsync(
        VisionAnalyzerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.ImageBytes is null || request.ImageBytes.Length == 0)
        {
            throw new InvalidOperationException("Image bytes are required for vision analysis.");
        }

        var signal = $"{request.FileName} {request.UserNote}".Trim().ToLowerInvariant();

        string category;
        IReadOnlyList<string> suggestedServices;

        if (ContainsAny(signal, "dashboard", "warning", "check engine", "engine light", "indicator"))
        {
            category = "DASHBOARD_WARNING";
            suggestedServices = ["Diagnostics", "Electrical Check"];
        }
        else if (ContainsAny(signal, "headlight", "taillight", "turn signal", "lamp", "light"))
        {
            category = "LIGHT_SYSTEM_ISSUE";
            suggestedServices = ["Electrical Check", "Lighting Service"];
        }
        else if (ContainsAny(signal, "scratch", "dent", "crack", "bumper", "damage", "paint"))
        {
            category = "BODY_DAMAGE";
            suggestedServices = ["Body Repair", "Paint Service", "Insurance Inspection"];
        }
        else
        {
            category = "GENERAL_VEHICLE_IMAGE";
            suggestedServices = ["General Inspection"];
        }

        var summary = category switch
        {
            "DASHBOARD_WARNING" => "Image appears to include a dashboard warning context.",
            "LIGHT_SYSTEM_ISSUE" => "Image appears related to a vehicle lighting issue.",
            "BODY_DAMAGE" => "Image appears to contain visible exterior damage.",
            _ => "Image uploaded successfully; specific issue could not be confidently inferred."
        };

        return Task.FromResult(new VisionAnalyzerResult
        {
            ImageSummary = summary,
            SuspectedCategory = category,
            SuggestedServiceCategories = suggestedServices
        });
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(text) || keywords.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < keywords.Length; i++)
        {
            var keyword = keywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
