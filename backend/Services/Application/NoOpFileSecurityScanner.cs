using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed class NoOpFileSecurityScanner : IFileSecurityScanner
{
    public Task<FileSecurityScanResult> ScanAsync(
        FileSecurityScanRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(FileSecurityScanResult.Clean("NO_OP"));
    }
}
