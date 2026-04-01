using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGarage.Data;
using System.Data;
using System.Globalization;

namespace MyGarage.Services
{
    public class AppointmentNoShowService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentNoShowService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(2);
        private bool? _schemaCompatible;
        private bool _hasEndTimeColumn;

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
                    await MarkNoShowsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error marking no-show appointments");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task MarkNoShowsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GarageContext>();

            if (!await IsSchemaCompatibleAsync(context, cancellationToken))
            {
                return;
            }

            var now = DateTime.UtcNow;
            var todayText = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var nowTimeText = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            var graceMinutes = (int)GracePeriod.TotalMinutes;

            var count = _hasEndTimeColumn
                ? await context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE a
SET a.Status = 'NO_SHOW',
    a.UpdatedAt = {now}
FROM dbo.Appointments a
WHERE a.Status IN ('PENDING', 'CONFIRMED')
  AND (
      TRY_CONVERT(date, a.ScheduledDate) < TRY_CONVERT(date, {todayText})
      OR (
          TRY_CONVERT(date, a.ScheduledDate) = TRY_CONVERT(date, {todayText})
          AND TRY_CONVERT(time, a.EndTime) IS NOT NULL
          AND DATEDIFF(MINUTE, TRY_CONVERT(time, a.EndTime), TRY_CONVERT(time, {nowTimeText})) >= {graceMinutes}
      )
  );", cancellationToken)
                : await context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE a
SET a.Status = 'NO_SHOW',
    a.UpdatedAt = {now}
FROM dbo.Appointments a
WHERE a.Status IN ('PENDING', 'CONFIRMED')
  AND TRY_CONVERT(date, a.ScheduledDate) < TRY_CONVERT(date, {todayText});", cancellationToken);

            if (count > 0)
            {
                _logger.LogInformation("Marked {Count} appointments as NO_SHOW", count);
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
                async Task<bool> HasColumnAsync(string columnName)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = @"
SELECT COUNT(1)
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
                    return Convert.ToInt32(result ?? 0) > 0;
                }

                var hasScheduledDate = await HasColumnAsync("ScheduledDate");
                var hasStatus = await HasColumnAsync("Status");
                _hasEndTimeColumn = await HasColumnAsync("EndTime");

                var compatible = hasScheduledDate && hasStatus;
                _schemaCompatible = compatible;
                if (!compatible)
                {
                    _logger.LogInformation("AppointmentNoShowService: required appointment columns not found (ScheduledDate/Status). Service will retry on next interval.");
                }
                else if (!_hasEndTimeColumn)
                {
                    _logger.LogWarning("Appointments.EndTime is missing. No-show service will use date-only overdue rule.");
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
