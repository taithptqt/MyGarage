using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyGarage.Common;
using MyGarage.Data;
using MyGarage.Interfaces;

namespace MyGarage.Services.Background;

public sealed class NotificationRetryBackgroundService : Microsoft.Extensions.Hosting.BackgroundService
{
    private const string ActionPending = "NOTIFY_RETRY_PENDING";
    private const string ActionDone = "NOTIFY_RETRY_DONE";
    private const string ActionDead = "NOTIFY_RETRY_DEAD";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationRetryBackgroundService> _logger;

    public NotificationRetryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationRetryBackgroundService> logger)
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
                await ProcessPendingAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification retry cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var rows = await db.AuditLogs
            .Where(x => x.Action == ActionPending)
            .OrderBy(x => x.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            var payload = ParsePayload(row.NewValue);
            if (payload == null || payload.Recipients.Count == 0 || string.IsNullOrWhiteSpace(payload.Subject))
            {
                row.Action = ActionDead;
                row.Timestamp = VietnamTime.Now;
                continue;
            }

            try
            {
                foreach (var recipient in payload.Recipients.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await emailService.SendEmailAsync(recipient.Trim(), payload.Subject, payload.Content ?? string.Empty);
                }

                row.Action = ActionDone;
                row.Timestamp = VietnamTime.Now;
            }
            catch
            {
                payload.RetryCount += 1;
                payload.LastErrorAt = VietnamTime.Now;
                row.NewValue = JsonSerializer.Serialize(payload);
                row.Timestamp = VietnamTime.Now;
                row.Action = payload.RetryCount >= payload.MaxRetries ? ActionDead : ActionPending;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static NotificationRetryPayload? ParsePayload(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<NotificationRetryPayload>(raw, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private sealed class NotificationRetryPayload
    {
        public string Subject { get; set; } = string.Empty;
        public string? Content { get; set; }
        public List<string> Recipients { get; set; } = new();
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; } = 5;
        public DateTime? LastErrorAt { get; set; }
    }
}
