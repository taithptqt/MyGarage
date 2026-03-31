using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Data;
using MyGarage.Interfaces;
using System.Data;

namespace MyGarage.Services
{
    public class AppointmentReminderService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentReminderService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
        private bool? _schemaCompatible;

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
                    await SendRemindersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending appointment reminders");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task SendRemindersAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GarageContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            if (!await IsSchemaCompatibleAsync(context, cancellationToken))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var tomorrow = DateOnly.FromDateTime(now.AddHours(24));

            var upcomingAppointments = await context.Appointments
                .Include(a => a.Customer)
                .Include(a => a.Branch)
                .Include(a => a.Vehicle)
                .Where(a => (a.Status == "PENDING" || a.Status == "CONFIRMED")
                    && a.IsReminderSent != true
                    && a.ScheduledDate <= tomorrow)
                .ToListAsync(cancellationToken);

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

        private async Task<bool> IsSchemaCompatibleAsync(GarageContext context, CancellationToken cancellationToken)
        {
            if (_schemaCompatible.HasValue)
            {
                return _schemaCompatible.Value;
            }

            var connection = context.Database.GetDbConnection();
            var wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                async Task<string?> GetColumnDataTypeAsync(string columnName)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = @"
SELECT DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = @tableName
  AND COLUMN_NAME = @columnName";

                    var tableNameParam = command.CreateParameter();
                    tableNameParam.ParameterName = "@tableName";
                    tableNameParam.Value = "Appointments";
                    command.Parameters.Add(tableNameParam);

                    var columnNameParam = command.CreateParameter();
                    columnNameParam.ParameterName = "@columnName";
                    columnNameParam.Value = columnName;
                    command.Parameters.Add(columnNameParam);

                    var result = await command.ExecuteScalarAsync(cancellationToken);
                    return result?.ToString();
                }

                var scheduledDateType = await GetColumnDataTypeAsync("ScheduledDate");
                var startTimeType = await GetColumnDataTypeAsync("StartTime");
                var endTimeType = await GetColumnDataTypeAsync("EndTime");

                var hasScheduledDate = !string.IsNullOrWhiteSpace(scheduledDateType);
                var timeColumnsCompatible =
                    string.Equals(startTimeType, "time", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(endTimeType, "time", StringComparison.OrdinalIgnoreCase);

                var compatible = hasScheduledDate && timeColumnsCompatible;
                _schemaCompatible = compatible;

                if (!compatible)
                {
                    _logger.LogWarning(
                        "AppointmentReminderService disabled because appointment schema is not compatible. ScheduledDateType={ScheduledDateType}, StartTimeType={StartTimeType}, EndTimeType={EndTimeType}. Expected StartTime/EndTime = time.",
                        scheduledDateType ?? "(missing)",
                        startTimeType ?? "(missing)",
                        endTimeType ?? "(missing)");
                }

                return compatible;
            }
            finally
            {
                if (wasClosed)
                {
                    await connection.CloseAsync();
                }
            }
        }
    }
}
