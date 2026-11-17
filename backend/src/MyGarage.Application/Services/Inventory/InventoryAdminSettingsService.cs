using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Common;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Services;

/// <summary>
/// Application service coordinating read/write operations for inventory admin settings.
/// </summary>
public sealed class InventoryAdminSettingsService : IInventoryAdminSettingsService
{
    private const string SettingsFileName = "inventory-admin-settings.json";
    private readonly IAppConfigFileStore _configFileStore;

    public InventoryAdminSettingsService(IAppConfigFileStore configFileStore)
    {
        _configFileStore = configFileStore;
    }

    public Task<InventoryAdminSettings> GetSettingsAsync(CancellationToken ct = default)
        => _configFileStore.ReadOrDefaultAsync(SettingsFileName, InventoryAdminSettings.CreateDefault, ct);

    public async Task<InventoryAdminSettings> SaveSettingsAsync(InventoryAdminSettings? settings, CancellationToken ct = default)
    {
        var sanitized = (settings ?? InventoryAdminSettings.CreateDefault()) with { UpdatedAt = VietnamTime.Now };
        await _configFileStore.WriteAsync(SettingsFileName, sanitized, ct);
        return sanitized;
    }
}
