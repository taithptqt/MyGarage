using Microsoft.Extensions.DependencyInjection;
using MyGarage.Interfaces;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public sealed class FileSecurityCompositionTests
{
    [Fact]
    public async Task RegisteredScanner_ResolvesFromInfrastructureAndRejectsSpoofedFile()
    {
        await using var factory = new TestWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var scanner = scope.ServiceProvider.GetRequiredService<IFileSecurityScanner>();

        Assert.IsType<MagicBytesFileSecurityScanner>(scanner);
        Assert.Equal("MyGarage.Application", typeof(IFileSecurityScanner).Assembly.GetName().Name);
        Assert.Equal("MyGarage.Infrastructure", scanner.GetType().Assembly.GetName().Name);
        var result = await scanner.ScanAsync(new FileSecurityScanRequest
        {
            FileName = "spoofed.png",
            ContentType = "image/png",
            FileBytes = [0x00, 0x01, 0x02]
        });
        Assert.False(result.IsClean);
        Assert.Equal("SIGNATURE_MISMATCH", result.DetectionCode);
    }
}
