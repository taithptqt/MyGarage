using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Application.Interfaces.Stores.Common;

namespace MyGarage.Infrastructure.Persistence.Stores.Common;

/// <summary>
/// Stores administrative JSON configuration snapshots under the host Configurations folder with per-file locking.
/// </summary>
public sealed class FileAppConfigFileStore : IAppConfigFileStore
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<FileAppConfigFileStore> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public FileAppConfigFileStore(IHostEnvironment environment, ILogger<FileAppConfigFileStore> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<T> ReadOrDefaultAsync<T>(string fileName, Func<T> createDefault, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(createDefault);

        var safeFileName = ValidateFileName(fileName);
        var path = ResolveSettingsPath(safeFileName);
        var gate = _locks.GetOrAdd(safeFileName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(path))
            {
                return createDefault();
            }

            try
            {
                await using var stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct)
                    ?? createDefault();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogWarning(ex, "Failed to read config settings from {SettingsPath}; falling back to defaults.", path);
                return createDefault();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<T> ReadStrictOrDefaultAsync<T>(string fileName, Func<T> createDefault, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(createDefault);
        var safeFileName = ValidateFileName(fileName);
        var path = ResolveSettingsPath(safeFileName);
        var gate = _locks.GetOrAdd(safeFileName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            try
            {
                await using var stream = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct)
                    ?? throw new InvalidDataException("Configuration is empty.");
            }
            catch (FileNotFoundException) { return createDefault(); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _logger.LogError(ex, "Failed to read config settings from {SettingsPath}.", path);
                throw new InvalidOperationException("Không đọc được dữ liệu cấu hình khoang; vui lòng kiểm tra dữ liệu lưu trữ.", ex);
            }
        }
        finally { gate.Release(); }
    }

    public async Task WriteAsync<T>(string fileName, T settings, CancellationToken ct = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(settings);

        var safeFileName = ValidateFileName(fileName);
        var path = ResolveSettingsPath(safeFileName);
        var gate = _locks.GetOrAdd(safeFileName, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (string.Equals(safeFileName, "bay-admin-settings.json", StringComparison.OrdinalIgnoreCase))
            {
                var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                    {
                        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, ct);
                        await stream.FlushAsync(ct);
                    }
                    File.Move(temporaryPath, path, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            else
            {
                await using var stream = File.Create(path);
                await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions, ct);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private string ResolveSettingsPath(string safeFileName)
    {
        var folder = Path.Combine(_environment.ContentRootPath, "Configurations");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, safeFileName);
    }

    private static string ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("Configuration file name must be a single valid file name.", nameof(fileName));
        }

        return fileName;
    }
}
