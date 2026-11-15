using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Services;

internal sealed class ProactiveMaintenanceOutreachJobStep
{
    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;
    private readonly CustomerAutomationTransactionRunner _transactionRunner;

    public ProactiveMaintenanceOutreachJobStep(
        IGarageContext context,
        ISystemClock clock,
        CustomerAutomationTransactionRunner transactionRunner)
    {
        _context = context;
        _clock = clock;
        _transactionRunner = transactionRunner;
    }

    public async Task<BackgroundJobRunResult> RunAsync(
        ProactiveMaintenanceOutreachOptions settings,
        CancellationToken cancellationToken)
    {
        const string jobName = "ProactiveMaintenanceOutreach";
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
        var completedStatuses = CustomerNotificationDedupeHelper.NormalizeList(
            settings.CompletedServiceStatuses,
            "CLOSED",
            "READY",
            "COMPLETED",
            "DONE");
        var forecastTypes = CustomerNotificationDedupeHelper.NormalizeList(
            settings.ForecastTypes,
            "MAINTENANCE",
            "SERVICE",
            "INSPECTION");
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

        var pendingCandidates = new List<MaintenanceNotificationCandidate>();
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

            pendingCandidates.Add(new MaintenanceNotificationCandidate
            {
                CustomerId = owner.CustomerId,
                VehicleId = owner.VehicleId,
                SemanticToken = dedupeToken,
                LinkAction = $"/my-vehicles/{owner.VehicleId}?tab=maintenance&notificationType=maintenance&dedupe={dedupeToken}",
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
                (EF.Functions.Like(n.LinkAction, $"{CustomerNotificationDedupeHelper.MaintenancePrefix}%") ||
                 EF.Functions.Like(n.LinkAction, "%notificationType=maintenance%")))
            .Select(n => new { n.CustomerId, n.LinkAction })
            .ToListAsync(cancellationToken);
        var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existingRows)
        {
            if (CustomerNotificationDedupeHelper.TryBuildMaintenanceNotificationKey(row.CustomerId, row.LinkAction!, out var existingKey))
            {
                existingKeys.Add(existingKey);
            }
        }

        var notifications = new List<Notification>();
        foreach (var candidate in pendingCandidates)
        {
            var key = CustomerNotificationDedupeHelper.BuildMaintenanceNotificationKey(
                candidate.CustomerId,
                candidate.VehicleId,
                candidate.SemanticToken);
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

        await _transactionRunner.ExecuteInTransactionAsync(async ct =>
        {
            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(ct);
            return 0;
        }, cancellationToken);

        result.NotifiedCount = notifications.Count;
        return result;
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

    private sealed class MaintenanceNotificationCandidate
    {
        public long CustomerId { get; set; }

        public long VehicleId { get; set; }

        public string SemanticToken { get; set; } = string.Empty;

        public string LinkAction { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
