using System.Collections.Generic;

namespace MyGarage.Interfaces;

public interface IVisionAnalyzer
{
    Task<VisionAnalyzerResult> AnalyzeAsync(
        VisionAnalyzerRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class VisionAnalyzerRequest
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] ImageBytes { get; set; } = [];

    public string? AttachmentUrl { get; set; }

    public string? UserNote { get; set; }
}

public sealed class VisionAnalyzerResult
{
    public string ImageSummary { get; set; } = string.Empty;

    public string SuspectedCategory { get; set; } = "UNKNOWN";

    public IReadOnlyList<string> SuggestedServiceCategories { get; set; } = [];
}
