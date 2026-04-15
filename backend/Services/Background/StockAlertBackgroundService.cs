using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services.Background;

public sealed class StockAlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StockAlertBackgroundService> _logger;

    public StockAlertBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<StockAlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IInventoryAdminService>();
                var alerts = await service.RebuildStockAlertsAsync(null);
                _logger.LogInformation("Stock alert background run completed. Active alerts: {Count}", alerts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stock alert background task failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}

