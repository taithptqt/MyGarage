using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Data;
using MyGarage.Interfaces;

namespace MyGarage.Services
{
    public class AppointmentReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentReminderService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

        public AppointmentReminderService(IServiceScopeFactory scopeFactory, ILogger<AppointmentReminderService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SendRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending appointment reminders");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task SendRemindersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GarageContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now = DateTime.UtcNow;
            var tomorrow = DateOnly.FromDateTime(now.AddHours(24));

            var upcomingAppointments = await context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Branch)
                .Include(a => a.Vehicle)
                .Where(a => (a.Status == "PENDING" || a.Status == "CONFIRMED")
                    && a.IsReminderSent != true
                    && a.ScheduledDate <= tomorrow)
                .ToListAsync();

            if (upcomingAppointments.Count == 0) return;

            var sent = 0;
            foreach (var a in upcomingAppointments)
            {
                var email = a.Customer?.Email;
                if (string.IsNullOrWhiteSpace(email)) continue;

                var customerName = a.Customer?.FullName ?? "Quy khach";
                var branchName = a.Branch?.Name ?? "Garage";
                var plate = a.Vehicle?.LicensePlate;
                var timeStr = $"{a.StartTime:HH\\:mm}";

                var subject = $"[MyGarage] Nhac lich hen - {a.ScheduledDate:dd/MM/yyyy}";
                var body = $@"
<div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px'>
  <h2 style='color:#422afb'>MyGarage - Nhac lich hen</h2>
  <p>Xin chao <strong>{customerName}</strong>,</p>
  <p>Ban co lich hen sap toi tai <strong>{branchName}</strong>:</p>
  <table style='border-collapse:collapse;width:100%;margin:16px 0'>
    <tr><td style='padding:8px;border:1px solid #e2e8f0;font-weight:600;width:120px'>Ngay</td>
        <td style='padding:8px;border:1px solid #e2e8f0'>{a.ScheduledDate:dd/MM/yyyy}</td></tr>
    <tr><td style='padding:8px;border:1px solid #e2e8f0;font-weight:600'>Gio</td>
        <td style='padding:8px;border:1px solid #e2e8f0'>{timeStr}</td></tr>
    {(plate != null ? $"<tr><td style='padding:8px;border:1px solid #e2e8f0;font-weight:600'>Bien so xe</td><td style='padding:8px;border:1px solid #e2e8f0'>{plate}</td></tr>" : "")}
  </table>
  <p>Vui long den dung gio. Neu can huy hoac doi lich, vui long thao tac tren App hoac lien he Hotline.</p>
  <p style='color:#718096;font-size:13px;margin-top:24px'>--- MyGarage System ---</p>
</div>";

                try
                {
                    await emailService.SendEmailAsync(email, subject, body);
                    a.IsReminderSent = true;
                    a.UpdatedAt = now;
                    sent++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send reminder to {Email} for appointment #{Id}", email, a.Id);
                }
            }

            if (sent > 0)
            {
                await context.SaveChangesAsync();
                _logger.LogInformation("Sent {Count} appointment reminders", sent);
            }
        }
    }
}
