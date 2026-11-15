using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Services;

internal sealed class PostServiceSurveyJobStep
{
    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;
    private readonly CustomerAutomationTransactionRunner _transactionRunner;

    public PostServiceSurveyJobStep(
        IGarageContext context,
        ISystemClock clock,
        CustomerAutomationTransactionRunner transactionRunner)
    {
        _context = context;
        _clock = clock;
        _transactionRunner = transactionRunner;
    }

    public async Task<BackgroundJobRunResult> RunAsync(
        PostServiceSurveyOptions settings,
        CancellationToken cancellationToken)
    {
        const string jobName = "PostServiceSurvey";
        if (!settings.Enabled)
        {
            return BackgroundJobRunResult.Disabled(jobName);
        }

        var completedStatuses = CustomerNotificationDedupeHelper.NormalizeList(
            settings.CompletedServiceStatuses,
            "CLOSED",
            "READY",
            "COMPLETED",
            "DONE");
        var cutoffUtc = _clock.UtcNow.AddHours(-settings.DelayHours);

        var candidateOrders = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so =>
                completedStatuses.Contains(so.Status) &&
                so.Customer.IsDeleted != true &&
                so.Customer.IsActive != false &&
                (so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt).HasValue &&
                (so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt) <= cutoffUtc)
            .Select(so => new SurveyCandidateRow
            {
                ServiceOrderId = so.Id,
                CustomerId = so.CustomerId,
                VehicleId = so.VehicleId,
                CompletionAt = (so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt)!.Value
            })
            .OrderBy(x => x.CompletionAt)
            .Take(settings.BatchSize * 3)
            .ToListAsync(cancellationToken);

        var result = new BackgroundJobRunResult
        {
            JobName = jobName,
            ExaminedCount = candidateOrders.Count
        };
        if (candidateOrders.Count == 0)
        {
            return result;
        }

        var orderIds = candidateOrders.Select(x => x.ServiceOrderId).Distinct().ToList();
        var feedbackOrderIds = await _context.CustomerFeedbacks
            .AsNoTracking()
            .Where(f => f.ServiceOrderId.HasValue && orderIds.Contains(f.ServiceOrderId.Value))
            .Select(f => f.ServiceOrderId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var feedbackSet = feedbackOrderIds.ToHashSet();

        var customerIds = candidateOrders.Select(x => x.CustomerId).Distinct().ToList();
        var existingSurveyRows = await _context.Notifications
            .AsNoTracking()
            .Where(n =>
                customerIds.Contains(n.CustomerId) &&
                n.LinkAction != null &&
                (EF.Functions.Like(n.LinkAction, $"{CustomerNotificationDedupeHelper.SurveyPrefix}%") ||
                 EF.Functions.Like(n.LinkAction, "%notificationType=survey%")))
            .Select(n => new { n.CustomerId, n.LinkAction })
            .ToListAsync(cancellationToken);
        var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existingSurveyRows)
        {
            if (CustomerNotificationDedupeHelper.TryBuildSurveyNotificationKey(row.CustomerId, row.LinkAction!, out var existingKey))
            {
                existingKeys.Add(existingKey);
            }
        }

        var notifications = new List<Notification>();
        foreach (var order in candidateOrders)
        {
            if (feedbackSet.Contains(order.ServiceOrderId))
            {
                result.SkippedCount++;
                continue;
            }

            var linkAction = $"/my-vehicles/{order.VehicleId}?tab=history&serviceOrderId={order.ServiceOrderId}&notificationType=survey";
            var key = CustomerNotificationDedupeHelper.BuildSurveyNotificationKey(order.CustomerId, order.ServiceOrderId);
            if (!existingKeys.Add(key))
            {
                result.SkippedCount++;
                continue;
            }

            notifications.Add(new Notification
            {
                CustomerId = order.CustomerId,
                Title = settings.NotificationTitle,
                Content = $"Please rate your experience for service order #{order.ServiceOrderId}. Your feedback helps us improve.",
                IsRead = false,
                SentAt = _clock.UtcNow,
                LinkAction = linkAction
            });

            if (notifications.Count >= settings.BatchSize)
            {
                break;
            }
        }

        if (notifications.Count == 0)
        {
            return result;
        }

        await _transactionRunner.ExecuteInTransactionAsync(async ct =>
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(ct);
            return 0;
        }, cancellationToken);

        result.NotifiedCount = notifications.Count;
        return result;
    }

    private sealed class SurveyCandidateRow
    {
        public long ServiceOrderId { get; set; }

        public long CustomerId { get; set; }

        public long VehicleId { get; set; }

        public DateTime CompletionAt { get; set; }
    }
}
