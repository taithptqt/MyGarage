using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Data;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AppointmentDispatchBackgroundService : BackgroundService
    {
        private static readonly TimeSpan DispatchInterval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan OvernightCutoff = new(18, 5, 0);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentDispatchBackgroundService> _logger;
        private readonly ConcurrentDictionary<long, DateOnly> _overnightProcessedByBranch = new();

        public AppointmentDispatchBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AppointmentDispatchBackgroundService> logger)
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
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<GarageContext>();
                    var service = scope.ServiceProvider.GetRequiredService<IAppointmentService>();

                    var now = DateTime.Now;
                    var branchIds = await context.Branches
                        .AsNoTracking()
                        .Where(b => b.IsDeleted != true)
                        .Select(b => b.Id)
                        .ToListAsync(stoppingToken);

                    foreach (var branchId in branchIds)
                    {
                        await service.RunDispatchCycleAsync(branchId, now);

                        var businessDate = DateOnly.FromDateTime(now);
                        if (now.TimeOfDay >= OvernightCutoff)
                        {
                            var processed = _overnightProcessedByBranch.TryGetValue(branchId, out var doneDate) && doneDate == businessDate;
                            if (!processed)
                            {
                                await service.LockOvernightSlotsAsync(branchId, businessDate);
                                _overnightProcessedByBranch[branchId] = businessDate;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Appointment dispatch background cycle failed.");
                }

                await Task.Delay(DispatchInterval, stoppingToken);
            }
        }
    }
}
