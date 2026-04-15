using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Interfaces;

namespace MyGarage.Services.Background
{
    public sealed class GrnSnapshotBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GrnSnapshotBackgroundService> _logger;

        public GrnSnapshotBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<GrnSnapshotBackgroundService> logger)
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
                    var targetDate = DateOnly.FromDateTime(now.Date.AddDays(-1));
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<IGrnSnapshotService>();
                    await service.RebuildDailySnapshotAsync(targetDate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GRN snapshot background task failed.");
                }

                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }
    }
}
