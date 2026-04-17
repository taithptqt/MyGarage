using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services.Background;

public sealed class MaintenanceReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceReminderBackgroundService> _logger;
    private DateOnly? _lastRunDate;

    public MaintenanceReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MaintenanceReminderBackgroundService> logger)
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
                var now = VietnamTime.Now;
                var today = DateOnly.FromDateTime(now.Date);
                if (now.Hour >= 8 && _lastRunDate != today)
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<IMaintenanceReminderService>();
                    var result = await service.ProcessDueAsync(500, stoppingToken);
                    _lastRunDate = today;
                    _logger.LogInformation("Maintenance reminder background run completed: {@Result}", result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Maintenance reminder background task failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
