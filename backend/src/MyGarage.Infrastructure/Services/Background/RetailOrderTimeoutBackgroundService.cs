using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Application.Interfaces.Queries;

namespace MyGarage.Services;

public sealed class RetailOrderTimeoutBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetailOrderTimeoutBackgroundService> _logger;

    public RetailOrderTimeoutBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RetailOrderTimeoutBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay on startup
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dataService = scope.ServiceProvider.GetRequiredService<IRetailOrderDataService>();
                var cancelledCount = await dataService.CancelExpiredDraftOrdersAsync(TimeSpan.FromMinutes(15), stoppingToken);
                if (cancelledCount > 0)
                {
                    _logger.LogInformation("RetailOrderTimeoutBackgroundService auto-cancelled {Count} expired draft retail orders.", cancelledCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while auto-cancelling expired draft retail orders.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
