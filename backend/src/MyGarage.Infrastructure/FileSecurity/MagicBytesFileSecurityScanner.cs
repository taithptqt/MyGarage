using System.Text;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class MagicBytesFileSecurityScanner : IFileSecurityScanner
{
    private const long MaxScanBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".svg", ".svgz", ".html", ".htm", ".xhtml", ".xml", ".js", ".mjs", ".cjs",
        ".exe", ".dll", ".bat", ".cmd", ".ps1", ".sh", ".vbs", ".php", ".asp", ".aspx", ".jsp"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
        ".pdf", ".xlsx", ".docx", ".zip", ".xls", ".doc", ".rar",
        ".txt", ".csv", ".mp4", ".webm", ".mov"
    };

    public Task<FileSecurityScanResult> ScanAsync(
        FileSecurityScanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || request.FileBytes == null || request.FileBytes.Length == 0)
        {
            return Task.FromResult(FileSecurityScanResult.Infected(
                "MAGIC_BYTES", "EMPTY_FILE", "Tệp tải lên rỗng."));
        }

        if (request.FileBytes.Length > MaxScanBytes)
        {
            return Task.FromResult(FileSecurityScanResult.Infected(
                "MAGIC_BYTES", "FILE_TOO_LARGE", "Tệp vượt quá dung lượng tối đa 20MB."));
        }

        var ext = Path.GetExtension(request.FileName ?? string.Empty).Trim();
        var contentType = (request.ContentType ?? string.Empty).Trim();

        if (BlockedExtensions.Contains(ext) ||
            contentType.Contains("svg", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
            contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(FileSecurityScanResult.Infected(
                "MAGIC_BYTES", "DANGEROUS_FILE_TYPE", "Định dạng tệp không được phép tải lên vì lý do bảo mật."));
        }

        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            return Task.FromResult(FileSecurityScanResult.Infected(
                "MAGIC_BYTES", "UNSUPPORTED_EXTENSION", $"Định dạng tệp '{ext}' không được hỗ trợ."));
        }

        if (ContainsEmbeddedScriptOrSvg(request.FileBytes))
        {
            return Task.FromResult(FileSecurityScanResult.Infected(
                "MAGIC_BYTES", "EMBEDDED_ACTIVE_CONTENT", "Phát hiện nội dung kịch bản/SVG nhúng trong tệp."));
        }

        if (!MatchesMagicBytes(ext, request.FileBytes))
        {
            return Task.FromResult(FileSecurityScanResult.Infected(
                "MAGIC_BYTES", "SIGNATURE_MISMATCH", $"Nội dung nhị phân của tệp '{ext}' không khớp chữ ký hợp lệ."));
        }

        return Task.FromResult(FileSecurityScanResult.Clean("MAGIC_BYTES"));
    }

    private static bool MatchesMagicBytes(string ext, byte[] bytes)
    {
        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ".png" => bytes.Length >= 8 &&
                      bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
                      bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            ".gif" => bytes.Length >= 6 &&
                      bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38 &&
                      (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61,
            ".webp" => bytes.Length >= 12 &&
                       bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                       bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50,
            ".bmp" => bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D,
            ".pdf" => bytes.Length >= 5 &&
                      bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D,
            ".xlsx" or ".docx" or ".zip" => bytes.Length >= 4 &&
                                             bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04,
            ".xls" or ".doc" => bytes.Length >= 8 &&
                                bytes[0] == 0xD0 && bytes[1] == 0xCF && bytes[2] == 0x11 && bytes[3] == 0xE0,
            ".rar" => bytes.Length >= 4 &&
                      bytes[0] == 0x52 && bytes[1] == 0x61 && bytes[2] == 0x72 && bytes[3] == 0x21,
            ".mp4" or ".mov" => bytes.Length >= 8 &&
                                bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70,
            ".webm" => bytes.Length >= 4 &&
                       bytes[0] == 0x1A && bytes[1] == 0x45 && bytes[2] == 0xDF && bytes[3] == 0xA3,
            ".txt" or ".csv" => !bytes.AsSpan(0, Math.Min(bytes.Length, 4096)).Contains((byte)0),
            _ => false
        };
    }

    private static bool ContainsEmbeddedScriptOrSvg(byte[] bytes)
    {
        var prefixLen = Math.Min(bytes.Length, 4096);
        var headerAscii = Encoding.UTF8.GetString(bytes, 0, prefixLen);
        return headerAscii.Contains("<svg", StringComparison.OrdinalIgnoreCase) ||
               headerAscii.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
               headerAscii.Contains("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase);
    }
}
