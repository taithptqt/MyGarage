using System.Threading;
using System.Threading.Tasks;
using MyGarage.DTOs.Responses;

namespace MyGarage.Interfaces;

/// <summary>
/// Manages persistent configuration settings for inventory administration.
/// </summary>
public interface IInventoryAdminSettingsService
{
    Task<InventoryAdminSettings> GetSettingsAsync(CancellationToken ct = default);
    Task<InventoryAdminSettings> SaveSettingsAsync(InventoryAdminSettings? settings, CancellationToken ct = default);
}
