namespace MyGarage.Interfaces;

public interface IFileSecurityScanner
{
    Task<FileSecurityScanResult> ScanAsync(
        FileSecurityScanRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class FileSecurityScanRequest
{
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] FileBytes { get; set; } = [];
}

public sealed class FileSecurityScanResult
{
    public bool IsClean { get; set; }

    public string? Engine { get; set; }

    public string? DetectionCode { get; set; }

    public string? DetectionMessage { get; set; }

    public static FileSecurityScanResult Clean(string? engine = null)
    {
        return new FileSecurityScanResult
        {
            IsClean = true,
            Engine = engine
        };
    }

    public static FileSecurityScanResult Infected(string? engine, string? code, string? message)
    {
        return new FileSecurityScanResult
        {
            IsClean = false,
            Engine = engine,
            DetectionCode = code,
            DetectionMessage = message
        };
    }
}
