using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Data;

namespace MyGarage.Services
{
    public class AppointmentNoShowService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentNoShowService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(2);

        public AppointmentNoShowService(IServiceScopeFactory scopeFactory, ILogger<AppointmentNoShowService> logger)
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
                    await MarkNoShowsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking no-show appointments");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task MarkNoShowsAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GarageContext>();

            var now = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var cutoffTime = TimeOnly.FromDateTime(now).AddHours(-2);

            var overdueAppointments = await context.Appointments
                .Where(a => (a.Status == "PENDING" || a.Status == "CONFIRMED")
                    && (a.ScheduledDate < today
                        || (a.ScheduledDate == today && a.EndTime.AddHours(2) <= TimeOnly.FromDateTime(now))))
                .ToListAsync();

            if (overdueAppointments.Count == 0) return;

            foreach (var a in overdueAppointments)
            {
                a.Status = "NO_SHOW";
                a.UpdatedAt = now;
            }

            var count = await context.SaveChangesAsync();
            if (count > 0)
                _logger.LogInformation("Marked {Count} appointments as NO_SHOW", overdueAppointments.Count);
        }
    }
}
