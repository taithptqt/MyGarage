using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Services;

internal sealed class ChatbotSessionCleanupJobStep
{
    private const string SessionCleanupActionType = "SESSION_CLEANUP";

    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;
    private readonly CustomerAutomationTransactionRunner _transactionRunner;

    public ChatbotSessionCleanupJobStep(
        IGarageContext context,
        ISystemClock clock,
        CustomerAutomationTransactionRunner transactionRunner)
    {
        _context = context;
        _clock = clock;
        _transactionRunner = transactionRunner;
    }

    public async Task<BackgroundJobRunResult> RunAsync(
        ChatbotSessionCleanupOptions settings,
        CancellationToken cancellationToken)
    {
        const string jobName = "ChatbotSessionCleanup";
        if (!settings.Enabled)
        {
            return BackgroundJobRunResult.Disabled(jobName);
        }

        var nowUtc = _clock.UtcNow;
        var expireBeforeUtc = nowUtc.AddHours(-settings.IdleToExpiredHours);
        var archiveBeforeUtc = nowUtc.AddDays(-settings.ExpiredToArchivedDays);

        return await _transactionRunner.ExecuteInTransactionAsync(async ct =>
        {
            var result = new BackgroundJobRunResult { JobName = jobName };

            var toExpire = await _context.ChatbotSessions
                .Where(session =>
                    (session.Status == nameof(ChatbotSessionStatus.ACTIVE) || session.Status == nameof(ChatbotSessionStatus.IDLE)) &&
                    session.LastActivityAt <= expireBeforeUtc)
                .OrderBy(session => session.LastActivityAt)
                .Take(settings.BatchSize)
                .ToListAsync(ct);

            foreach (var session in toExpire)
            {
                session.Status = nameof(ChatbotSessionStatus.EXPIRED);
                session.ClosedAt ??= nowUtc;
                session.UpdatedAt = nowUtc;

                _context.ChatbotActionLogs.Add(new ChatbotActionLog
                {
                    SessionId = session.Id,
                    ActionType = SessionCleanupActionType,
                    Status = "SUCCESS",
                    ActionPayload = JsonSerializer.Serialize(new
                    {
                        transition = "ACTIVE_OR_IDLE_TO_EXPIRED",
                        cleanup_at_utc = nowUtc
                    }),
                    CreatedAt = nowUtc
                });
            }

            var toArchive = await _context.ChatbotSessions
                .Where(session =>
                    session.Status == nameof(ChatbotSessionStatus.EXPIRED) &&
                    (session.ClosedAt ?? session.LastActivityAt) <= archiveBeforeUtc)
                .OrderBy(session => session.ClosedAt ?? session.LastActivityAt)
                .Take(settings.BatchSize)
                .ToListAsync(ct);

            foreach (var session in toArchive)
            {
                session.Status = nameof(ChatbotSessionStatus.ARCHIVED);
                session.UpdatedAt = nowUtc;

                _context.ChatbotActionLogs.Add(new ChatbotActionLog
                {
                    SessionId = session.Id,
                    ActionType = SessionCleanupActionType,
                    Status = "SUCCESS",
                    ActionPayload = JsonSerializer.Serialize(new
                    {
                        transition = "EXPIRED_TO_ARCHIVED",
                        cleanup_at_utc = nowUtc
                    }),
                    CreatedAt = nowUtc
                });
            }

            result.ExaminedCount = toExpire.Count + toArchive.Count;
            result.UpdatedCount = toExpire.Count + toArchive.Count;
            if (result.UpdatedCount > 0)
            {
                await _context.SaveChangesAsync(ct);
            }

            return result;
        }, cancellationToken);
    }
}
