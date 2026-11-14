using Microsoft.Extensions.Options;

namespace MyGarage.Options;

public sealed class CustomerAutomationJobsOptionsValidator : IValidateOptions<CustomerAutomationJobsOptions>
{
    public ValidateOptionsResult Validate(string? name, CustomerAutomationJobsOptions options)
    {
        var errors = new List<string>();

        ValidateRange(errors, nameof(options.StartupDelaySeconds), options.StartupDelaySeconds, 0, 3600);
        ValidateRange(errors, nameof(options.LoopIntervalSeconds), options.LoopIntervalSeconds, 30, 86400);

        ValidateRange(errors, "Retry.MaxAttempts", options.Retry.MaxAttempts, 1, 10);
        ValidateRange(errors, "Retry.BaseDelaySeconds", options.Retry.BaseDelaySeconds, 1, 300);

        ValidateRange(errors, "MaintenanceOutreach.BatchSize", options.MaintenanceOutreach.BatchSize, 1, 5000);
        ValidateRange(errors, "MaintenanceOutreach.LastCompletedServiceAgeDays", options.MaintenanceOutreach.LastCompletedServiceAgeDays, 1, 3650);
        ValidateRange(errors, "MaintenanceOutreach.OdoDeltaThreshold", options.MaintenanceOutreach.OdoDeltaThreshold, 1, 1_000_000);
        ValidateRange(errors, "MaintenanceOutreach.ForecastLookaheadDays", options.MaintenanceOutreach.ForecastLookaheadDays, 1, 3650);
        ValidateNonEmptyStringList(errors, "MaintenanceOutreach.CompletedServiceStatuses", options.MaintenanceOutreach.CompletedServiceStatuses);

        ValidateRange(errors, "PostServiceSurvey.BatchSize", options.PostServiceSurvey.BatchSize, 1, 5000);
        ValidateRange(errors, "PostServiceSurvey.DelayHours", options.PostServiceSurvey.DelayHours, 1, 24 * 365);
        ValidateNonEmptyStringList(errors, "PostServiceSurvey.CompletedServiceStatuses", options.PostServiceSurvey.CompletedServiceStatuses);

        ValidateRange(errors, "InsuranceExpiryReminder.BatchSize", options.InsuranceExpiryReminder.BatchSize, 1, 5000);
        ValidateRange(errors, "InsuranceExpiryReminder.ReminderWindowDays", options.InsuranceExpiryReminder.ReminderWindowDays, 1, 3650);

        ValidateRange(errors, "ChatbotSessionCleanup.BatchSize", options.ChatbotSessionCleanup.BatchSize, 1, 10000);
        ValidateRange(errors, "ChatbotSessionCleanup.IdleToExpiredHours", options.ChatbotSessionCleanup.IdleToExpiredHours, 1, 24 * 365);
        ValidateRange(errors, "ChatbotSessionCleanup.ExpiredToArchivedDays", options.ChatbotSessionCleanup.ExpiredToArchivedDays, 1, 3650);

        return errors.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(errors);
    }

    private static void ValidateRange(
        ICollection<string> errors,
        string key,
        int value,
        int min,
        int max)
    {
        if (value < min || value > max)
        {
            errors.Add($"{CustomerAutomationJobsOptions.SectionName}:{key} must be between {min} and {max}.");
        }
    }

    private static void ValidateNonEmptyStringList(
        ICollection<string> errors,
        string key,
        IReadOnlyCollection<string>? values)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"{CustomerAutomationJobsOptions.SectionName}:{key} must contain at least one non-empty value.");
        }
    }
}
