using Microsoft.EntityFrameworkCore;
using MyGarage.Interfaces;
using MyGarage.Models;
using MyGarage.Options;

namespace MyGarage.Services;

internal sealed class InsuranceExpiryReminderJobStep
{
    private readonly IGarageContext _context;
    private readonly ISystemClock _clock;
    private readonly CustomerAutomationTransactionRunner _transactionRunner;

    public InsuranceExpiryReminderJobStep(
        IGarageContext context,
        ISystemClock clock,
        CustomerAutomationTransactionRunner transactionRunner)
    {
        _context = context;
        _clock = clock;
        _transactionRunner = transactionRunner;
    }

    public async Task<BackgroundJobRunResult> RunAsync(
        InsuranceExpiryReminderOptions settings,
        CancellationToken cancellationToken)
    {
        const string jobName = "InsuranceExpiryReminder";
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

        var pending = new List<InsuranceNotificationCandidate>();
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
                pending.Add(new InsuranceNotificationCandidate
                {
                    CustomerId = customerId,
                    VehicleInsuranceId = insurance.VehicleInsuranceId,
                    ExpiryDate = insurance.ExpiryDate,
                    LinkAction = $"/my-vehicles/{insurance.VehicleId}?tab=overview&notificationType=insurance&vehicleInsuranceId={insurance.VehicleInsuranceId}&expiryDate={insurance.ExpiryDate:yyyyMMdd}",
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
                (EF.Functions.Like(n.LinkAction, $"{CustomerNotificationDedupeHelper.InsurancePrefix}%") ||
                 EF.Functions.Like(n.LinkAction, "%notificationType=insurance%")))
            .Select(n => new { n.CustomerId, n.LinkAction })
            .ToListAsync(cancellationToken);
        var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existingRows)
        {
            if (CustomerNotificationDedupeHelper.TryBuildInsuranceNotificationKey(row.CustomerId, row.LinkAction!, out var existingKey))
            {
                existingKeys.Add(existingKey);
            }
        }

        var notifications = new List<Notification>();
        foreach (var candidate in pending)
        {
            var key = CustomerNotificationDedupeHelper.BuildInsuranceNotificationKey(
                candidate.CustomerId,
                candidate.VehicleInsuranceId,
                candidate.ExpiryDate);
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

    private sealed class InsuranceCandidateRow
    {
        public long VehicleInsuranceId { get; set; }

        public long VehicleId { get; set; }

        public string PolicyNumber { get; set; } = string.Empty;

        public DateOnly ExpiryDate { get; set; }

        public string? VehicleLicensePlate { get; set; }
    }

    private sealed class InsuranceNotificationCandidate
    {
        public long CustomerId { get; set; }

        public long VehicleInsuranceId { get; set; }

        public DateOnly ExpiryDate { get; set; }

        public string LinkAction { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }
}
