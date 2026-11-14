using Microsoft.EntityFrameworkCore;
using MyGarage.Constants;
using MyGarage.Interfaces;
using MyGarage.Models;

namespace MyGarage.Services;

public class AppointmentInsuranceUploadCleanupBackgroundService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxStagedLifetime = TimeSpan.FromHours(12);
    private const int CleanupBatchSize = 50;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentInsuranceUploadCleanupBackgroundService> _logger;
    private readonly ISystemClock _clock;

    public AppointmentInsuranceUploadCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppointmentInsuranceUploadCleanupBackgroundService> logger,
        ISystemClock clock)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupStagedUploadsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể dọn ảnh bảo hiểm upload dở dang.");
            }

            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task CleanupStagedUploadsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IGarageContext>();
        var cloudStorageService = scope.ServiceProvider.GetRequiredService<ICloudStorageService>();
        var cutoff = _clock.LocalNow.Subtract(MaxStagedLifetime);

        var staleUploads = await context.AppointmentInsuranceUploads
            .Where(upload =>
                upload.Status == AppointmentInsuranceUploadStatuses.Staged &&
                upload.AppointmentId == null &&
                upload.CreatedAt.HasValue &&
                upload.CreatedAt.Value < cutoff)
            .OrderBy(upload => upload.CreatedAt)
            .Take(CleanupBatchSize)
            .ToListAsync(cancellationToken);

        if (staleUploads.Count == 0)
        {
            return;
        }

        var cleanedCount = 0;
        foreach (var upload in staleUploads)
        {
            try
            {
                await cloudStorageService.DeleteAsync(upload.BlobPath, cancellationToken);
                upload.Status = AppointmentInsuranceUploadStatuses.Deleted;
                upload.UpdatedAt = _clock.UtcNow;
                cleanedCount += 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Không thể xóa ảnh bảo hiểm upload dở dang #{UploadId} ({BlobPath}).",
                    upload.Id,
                    upload.BlobPath);
            }
        }

        if (cleanedCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Đã dọn {Count} ảnh bảo hiểm upload dở dang.", cleanedCount);
        }
    }
}
