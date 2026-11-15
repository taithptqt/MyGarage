using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Interfaces.Common;
using MyGarage.Application.Interfaces.Notifications;
using Xunit;

namespace MyGarage.Tests;

public sealed class HealthCheckBoundaryTests
{
    [Fact]
    public async Task HealthReady_Returns200WithReadyStatus()
    {
        await using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal("ready", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("connected", doc.RootElement.GetProperty("database").GetString());

        using var scope = factory.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<IDatabaseReadinessCheck>());
        Assert.NotNull(scope.ServiceProvider.GetService<IDiagnosisRealtimeNotifier>());
    }

    [Fact]
    public void ApiProject_HasZeroReferencesToGarageContextOrMyGarageData()
    {
        var apiDir = FindApiSourceDirectory();
        var csFiles = Directory.EnumerateFiles(apiDir, "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(csFiles);

        var violations = new List<string>();
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            if (content.Contains("GarageContext", StringComparison.Ordinal) ||
                content.Contains("MyGarage.Data", StringComparison.Ordinal))
            {
                violations.Add(Path.GetRelativePath(apiDir, file));
            }
        }

        Assert.Empty(violations);
    }

    private static string FindApiSourceDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "backend", "src", "MyGarage.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(dir.FullName, "src", "MyGarage.Api");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate backend/src/MyGarage.Api from test base directory.");
    }
}
