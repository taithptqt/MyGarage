using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MyGarage.Infrastructure.Persistence.Stores.Common;
using Xunit;

namespace MyGarage.Tests;

public sealed class BaySettingsStorageTests
{
    [Fact]
    public async Task CorruptSettings_DoNotFallBackOrOverwriteData()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyGarageBaySettings_" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "Configurations");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "bay-admin-settings.json");
        const string invalid = "{ invalid-json: [";
        await File.WriteAllTextAsync(path, invalid);
        try
        {
            var store = new FileAppConfigFileStore(new TestEnvironment(root), NullLogger<FileAppConfigFileStore>.Instance);
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.ReadStrictOrDefaultAsync("bay-admin-settings.json", () => new Dictionary<string, object>()));
            Assert.Equal(invalid, await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); Directory.Delete(folder); Directory.Delete(root); }
    }

    [Fact]
    public async Task FailedSerialization_KeepsPreviousFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "MyGarageBaySettings_" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "Configurations");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "bay-admin-settings.json");
        const string original = "{\"sessions\":[]}";
        await File.WriteAllTextAsync(path, original);
        try
        {
            var store = new FileAppConfigFileStore(new TestEnvironment(root), NullLogger<FileAppConfigFileStore>.Instance);
            var cycle = new Dictionary<string, object>();
            cycle["self"] = cycle;
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => store.WriteAsync("bay-admin-settings.json", cycle));
            Assert.Equal(original, await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(folder));
        }
        finally { File.Delete(path); Directory.Delete(folder); Directory.Delete(root); }
    }

    private sealed class TestEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "MyGarage.Tests";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
