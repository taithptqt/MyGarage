using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyGarage.Enums;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Services;

public sealed class CustomerAutomationJobProcessor : ICustomerAutomationJobProcessor
{
    private const string SessionCleanupActionType = "SESSION_CLEANUP";
    private const string MaintenancePrefix = "/jobs/maintenance/";
    private const string SurveyPrefix = "/jobs/survey/service-order/";
    private const string InsurancePrefix = "/jobs/insurance/";

    private readonly IGarageContext _context;
    private readonly IOptions<CustomerAutomationJobsOptions> _options;
    private readonly ISystemClock _clock;
    private readonly ILogger<CustomerAutomationJobProcessor> _logger;

    public CustomerAutomationJobProcessor(
        IGarageContext context,
        IOptions<CustomerAutomationJobsOptions> options,
        ISystemClock clock,
        ILogger<CustomerAutomationJobProcessor> logger)
    {
        _context = context;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BackgroundJobRunResult> RunProactiveMaintenanceOutreachAsync(CancellationToken cancellationToken = default)
    {
        const string jobName = "ProactiveMaintenanceOutreach";
        var settings = _options.Value.MaintenanceOutreach;
        if (!settings.Enabled)
        {
            return BackgroundJobRunResult.Disabled(jobName);
        }

        var ownerRows = await _context.CustomerVehicles
            .AsNoTracking()
            .Where(cv =>
                cv.IsDeleted != true &&
                cv.IsCurrentOwner != false &&
                cv.Customer.IsDeleted != true &&
                cv.Customer.IsActive != false &&
                cv.Vehicle.IsDeleted != true)
            .Select(cv => new MaintenanceOwnerRow
            {
                CustomerId = cv.CustomerId,
                VehicleId = cv.VehicleId,
                VehicleLicensePlate = cv.Vehicle.LicensePlate,
                CurrentOdo = cv.Vehicle.Odo
            })
            .ToListAsync(cancellationToken);

        var result = new BackgroundJobRunResult
        {
            JobName = jobName,
            ExaminedCount = ownerRows.Count
        };
        if (ownerRows.Count == 0)
        {
            return result;
        }

        var vehicleIds = ownerRows.Select(x => x.VehicleId).Distinct().ToList();
        var completedStatuses = NormalizeList(settings.CompletedServiceStatuses, "CLOSED", "READY", "COMPLETED", "DONE");
        var forecastTypes = NormalizeList(settings.ForecastTypes, "MAINTENANCE", "SERVICE", "INSPECTION");
        var staleServiceCutoffUtc = _clock.UtcNow.AddDays(-settings.LastCompletedServiceAgeDays);
        var today = _clock.LocalToday;
        var forecastWindowEnd = today.AddDays(settings.ForecastLookaheadDays);

        var latestServiceRows = await _context.ServiceOrders
            .AsNoTracking()
            .Where(so => vehicleIds.Contains(so.VehicleId) && completedStatuses.Contains(so.Status))
            .Select(so => new
            {
                so.VehicleId,
                CompletedAt = so.ActualHandover ?? so.UpdatedAt ?? so.CreatedAt
            })
            .Where(x => x.CompletedAt.HasValue)
            .ToListAsync(cancellationToken);
        var latestCompletedByVehicle = latestServiceRows
            .GroupBy(x => x.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.Max(x => x.CompletedAt!.Value));

        var serviceOdoRows = await _context.VehicleConditionLogs
            .AsNoTracking()
            .Where(vcl =>
                vcl.OdoReading.HasValue &&
                vehicleIds.Contains(vcl.ServiceOrder.VehicleId) &&
                completedStatuses.Contains(vcl.ServiceOrder.Status))
            .Select(vcl => new
            {
                VehicleId = vcl.ServiceOrder.VehicleId,
                OdoReading = vcl.OdoReading!.Value
            })
            .ToListAsync(cancellationToken);
        var serviceOdoByVehicle = serviceOdoRows
            .GroupBy(x => x.VehicleId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.OdoReading));

        var forecastRows = await _context.AiForecasts
            .AsNoTracking()
            .Where(f =>
                vehicleIds.Contains(f.VehicleId) &&
                f.PredictedDate >= today &&
                f.PredictedDate <= forecastWindowEnd &&
                forecastTypes.Contains(f.Type))
            .Select(f => new MaintenanceForecastRow
            {
                ForecastId = f.Id,
                VehicleId = f.VehicleId,
                ForecastType = f.Type,
                PredictedDate = f.PredictedDate
            })
            .ToListAsync(cancellationToken);
        var forecastByVehicle = forecastRows
            .GroupBy(x => x.VehicleId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.PredictedDate).ThenBy(x => x.ForecastId).First());

        var pendingCandidates = new List<NotificationCandidate>();
        foreach (var owner in ownerRows)
        {
            var dueByServiceAge = latestCompletedByVehicle.TryGetValue(owner.VehicleId, out var lastCompletedAtUtc)
                && lastCompletedAtUtc <= staleServiceCutoffUtc;
            var dueByOdo = serviceOdoByVehicle.TryGetValue(owner.VehicleId, out var lastServiceOdo)
                && owner.CurrentOdo.HasValue
                && owner.CurrentOdo.Value - lastServiceOdo >= settings.OdoDeltaThreshold;
            var hasForecast = forecastByVehicle.TryGetValue(owner.VehicleId, out var forecast);

            if (!dueByServiceAge && !dueByOdo && !hasForecast)
            {
                continue;
            }

            var reasons = new List<string>();
            if (dueByServiceAge)
            {
                reasons.Add($"last completed service older than {settings.LastCompletedServiceAgeDays} day(s)");
            }

            if (dueByOdo && owner.CurrentOdo.HasValue)
            {
                reasons.Add($"ODO delta reached {owner.CurrentOdo.Value - lastServiceOdo} km");
            }

            if (hasForecast && forecast is not null)
            {
                reasons.Add($"AI forecast ({forecast.ForecastType}) due by {forecast.PredictedDate:yyyy-MM-dd}");
            }

            var dedupeToken = hasForecast && forecast is not null
                ? $"forecast-{forecast.ForecastId}"
                : dueByOdo
                    ? $"odo-{lastServiceOdo}"
                    : $"service-{lastCompletedAtUtc:yyyyMMdd}";

            var vehicleLabel = string.IsNullOrWhiteSpace(owner.VehicleLicensePlate)
                ? $"Vehicle #{owner.VehicleId}"
                : owner.VehicleLicensePlate;

            pendingCandidates.Add(new NotificationCandidate
            {
                CustomerId = owner.CustomerId,
                LinkAction = $"{MaintenancePrefix}{owner.VehicleId}/{dedupeToken}",
                Title = settings.NotificationTitle,
                Content = $"Maintenance check recommended for {vehicleLabel}. Trigger(s): {string.Join("; ", reasons)}."
            });
        }

        if (pendingCandidates.Count == 0)
        {
            result.SkippedCount = result.ExaminedCount;
            return result;
        }

        var customerIds = pendingCandidates.Select(x => x.CustomerId).Distinct().ToList();
        var existingRows = await _context.Notifications
            .AsNoTracking()
            .Where(n =>
                customerIds.Contains(n.CustomerId) &&
                n.LinkAction != null &&
                EF.Functions.Like(n.LinkAction, $"{MaintenancePrefix}%"))
            .Select(n => new { n.CustomerId, n.LinkAction })
            .ToListAsync(cancellationToken);
        var existingKeys = new HashSet<string>(
            existingRows.Select(x => BuildNotificationKey(x.CustomerId, x.LinkAction!)),
            StringComparer.OrdinalIgnoreCase);

        var notifications = new List<Notification>();
        foreach (var candidate in pendingCandidates)
        {
            var key = BuildNotificationKey(candidate.CustomerId, candidate.LinkAction);
            if (!existingKeys.Add(key))
            {
                result.SkippedCount++;
                continue;
            }

            notifications.Add(new Notification
            {
                CustomerId = candidate.CustomerId,
                Title = candidate.Title,
                Content = candidate.Content,
                IsRead = false,
                SentAt = _clock.UtcNow,
                LinkAction = candidate.LinkAction
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

        await ExecuteInTransactionAsync(async ct =>
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(ct);
            return 0;
        }, cancellationToken);

        result.NotifiedCount = notifications.Count;
        return result;
    }

    public async Task<BackgroundJobRunResult> RunPostServiceSurveyAsync(CancellationToken cancellationToken = default)
    {
        const string jobName = "PostServiceSurvey";
        var settings = _options.Value.PostServiceSurvey;
        if (!settings.Enabled)
        {
            return BackgroundJobRunResult.Disabled(jobName);
        }

        var completedStatuses = NormalizeList(settings.CompletedServiceStatuses, "CLOSED", "READY", "COMPLETED", "DONE");
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
                EF.Functions.Like(n.LinkAction, $"{SurveyPrefix}%"))
            .Select(n => new { n.CustomerId, n.LinkAction })
            .ToListAsync(cancellationToken);
        var existingKeys = new HashSet<string>(
            existingSurveyRows.Select(x => BuildNotificationKey(x.CustomerId, x.LinkAction!)),
            StringComparer.OrdinalIgnoreCase);

        var notifications = new List<Notification>();
        foreach (var order in candidateOrders)
        {
            if (feedbackSet.Contains(order.ServiceOrderId))
            {
                result.SkippedCount++;
                continue;
            }

            var linkAction = $"{SurveyPrefix}{order.ServiceOrderId}";
            var key = BuildNotificationKey(order.CustomerId, linkAction);
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

        await ExecuteInTransactionAsync(async ct =>
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(ct);
            return 0;
        }, cancellationToken);

        result.NotifiedCount = notifications.Count;
        return result;
    }

    public async Task<BackgroundJobRunResult> RunInsuranceExpiryReminderAsync(CancellationToken cancellationToken = default)
    {
        const string jobName = "InsuranceExpiryReminder";
        var settings = _options.Value.InsuranceExpiryReminder;
        if (!settings.Enabled)
        {
            return BackgroundJobRunResult.Disabled(jobName);
        }

        var today = _clock.LocalToday;
        var reminderCutoff = today.AddDays(settings.ReminderWindowDays);

        var insuranceRows = await _context.VehicleInsurances
            .AsNoTracking()
            .Where(vi =>
                vi.Vehicle.IsDeleted != true &&
                vi.ExpiryDate >= today &&
                vi.ExpiryDate <= reminderCutoff)
            .Select(vi => new InsuranceCandidateRow
            {
                VehicleInsuranceId = vi.Id,
                VehicleId = vi.VehicleId,
                PolicyNumber = vi.PolicyNumber,
                ExpiryDate = vi.ExpiryDate,
                VehicleLicensePlate = vi.Vehicle.LicensePlate
            })
            .Take(settings.BatchSize * 5)
            .ToListAsync(cancellationToken);

        var result = new BackgroundJobRunResult
        {
            JobName = jobName,
            ExaminedCount = insuranceRows.Count
        };
        if (insuranceRows.Count == 0)
        {
            return result;
        }

        var vehicleIds = insuranceRows.Select(x => x.VehicleId).Distinct().ToList();
        var ownerRows = await _context.CustomerVehicles
            .AsNoTracking()
            .Where(cv =>
                vehicleIds.Contains(cv.VehicleId) &&
                cv.IsDeleted != true &&
                cv.IsCurrentOwner != false &&
                cv.Customer.IsDeleted != true &&
                cv.Customer.IsActive != false)
            .Select(cv => new
            {
                cv.CustomerId,
                cv.VehicleId
            })
            .ToListAsync(cancellationToken);

        var ownersByVehicle = ownerRows
            .GroupBy(x => x.VehicleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CustomerId).Distinct().ToList());

        var pending = new List<NotificationCandidate>();
        foreach (var insurance in insuranceRows)
        {
            if (!ownersByVehicle.TryGetValue(insurance.VehicleId, out var ownerCustomerIds))
            {
                result.SkippedCount++;
                continue;
            }

            var daysLeft = insurance.ExpiryDate.DayNumber - today.DayNumber;
            var vehicleLabel = string.IsNullOrWhiteSpace(insurance.VehicleLicensePlate)
                ? $"Vehicle #{insurance.VehicleId}"
                : insurance.VehicleLicensePlate;

            foreach (var customerId in ownerCustomerIds)
            {
                pending.Add(new NotificationCandidate
                {
                    CustomerId = customerId,
                    LinkAction = $"{InsurancePrefix}{insurance.VehicleInsuranceId}/{insurance.ExpiryDate:yyyyMMdd}",
                    Title = settings.NotificationTitle,
                    Content = $"Insurance policy {insurance.PolicyNumber} for {vehicleLabel} expires in {daysLeft} day(s) on {insurance.ExpiryDate:yyyy-MM-dd}."
                });
            }
        }

        if (pending.Count == 0)
        {
            return result;
        }

        var pendingCustomerIds = pending.Select(x => x.CustomerId).Distinct().ToList();
        var existingRows = await _context.Notifications
            .AsNoTracking()
            .Where(n =>
                pendingCustomerIds.Contains(n.CustomerId) &&
                n.LinkAction != null &&
                EF.Functions.Like(n.LinkAction, $"{InsurancePrefix}%"))
            .Select(n => new { n.CustomerId, n.LinkAction })
            .ToListAsync(cancellationToken);
        var existingKeys = new HashSet<string>(
            existingRows.Select(x => BuildNotificationKey(x.CustomerId, x.LinkAction!)),
            StringComparer.OrdinalIgnoreCase);

        var notifications = new List<Notification>();
        foreach (var candidate in pending)
        {
            var key = BuildNotificationKey(candidate.CustomerId, candidate.LinkAction);
            if (!existingKeys.Add(key))
            {
                result.SkippedCount++;
                continue;
            }

            notifications.Add(new Notification
            {
                CustomerId = candidate.CustomerId,
                Title = candidate.Title,
                Content = candidate.Content,
                IsRead = false,
                SentAt = _clock.UtcNow,
                LinkAction = candidate.LinkAction
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

        await ExecuteInTransactionAsync(async ct =>
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(ct);
            return 0;
        }, cancellationToken);

        result.NotifiedCount = notifications.Count;
        return result;
    }

    public async Task<BackgroundJobRunResult> RunChatbotSessionCleanupAsync(CancellationToken cancellationToken = default)
    {
        const string jobName = "ChatbotSessionCleanup";
        var settings = _options.Value.ChatbotSessionCleanup;
        if (!settings.Enabled)
        {
            return BackgroundJobRunResult.Disabled(jobName);
        }

        var nowUtc = _clock.UtcNow;
        var expireBeforeUtc = nowUtc.AddHours(-settings.IdleToExpiredHours);
        var archiveBeforeUtc = nowUtc.AddDays(-settings.ExpiredToArchivedDays);

        return await ExecuteInTransactionAsync(async ct =>
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

    private async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await TryBeginTransactionAsync(cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                if (tx is not null)
                {
                    await tx.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch (Exception ex)
            {
                if (tx is not null)
                {
                    await tx.RollbackAsync(cancellationToken);
                }

                _logger.LogError(ex, "Customer automation transactional operation failed.");
                throw;
            }
        });
    }

    private async Task<IDbContextTransaction?> TryBeginTransactionAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_context.Database.IsRelational())
            {
                return await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            }

            return await _context.Database.BeginTransactionAsync(cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string BuildNotificationKey(long customerId, string linkAction)
    {
        return $"{customerId}:{linkAction.Trim()}";
    }

    private static List<string> NormalizeList(IEnumerable<string>? values, params string[] defaults)
    {
        var source = values?.ToList() ?? defaults.ToList();
        var normalized = source
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalized.Count > 0 ? normalized : defaults.ToList();
    }

    private sealed class MaintenanceOwnerRow
    {
        public long CustomerId { get; set; }

        public long VehicleId { get; set; }

        public string? VehicleLicensePlate { get; set; }

        public int? CurrentOdo { get; set; }
    }

    private sealed class MaintenanceForecastRow
    {
        public long ForecastId { get; set; }

        public long VehicleId { get; set; }

        public string ForecastType { get; set; } = string.Empty;

        public DateOnly PredictedDate { get; set; }
    }

    private sealed class SurveyCandidateRow
    {
        public long ServiceOrderId { get; set; }

        public long CustomerId { get; set; }

        public DateTime CompletionAt { get; set; }
    }

    private sealed class InsuranceCandidateRow
    {
        public long VehicleInsuranceId { get; set; }

        public long VehicleId { get; set; }

        public string PolicyNumber { get; set; } = string.Empty;

        public DateOnly ExpiryDate { get; set; }

        public string? VehicleLicensePlate { get; set; }
    }

    private sealed class NotificationCandidate
    {
        public long CustomerId { get; set; }

        public string LinkAction { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
