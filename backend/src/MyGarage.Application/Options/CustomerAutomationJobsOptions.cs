using System.Collections.Generic;

namespace MyGarage.Options;

public sealed class CustomerAutomationJobsOptions
{
    public const string SectionName = "CustomerAutomationJobs";

    public bool Enabled { get; set; } = true;

    public int StartupDelaySeconds { get; set; } = 20;

    public int LoopIntervalSeconds { get; set; } = 300;

    public BackgroundJobRetryOptions Retry { get; set; } = new();

    public ProactiveMaintenanceOutreachOptions MaintenanceOutreach { get; set; } = new();

    public PostServiceSurveyOptions PostServiceSurvey { get; set; } = new();

    public InsuranceExpiryReminderOptions InsuranceExpiryReminder { get; set; } = new();

    public ChatbotSessionCleanupOptions ChatbotSessionCleanup { get; set; } = new();
}

public sealed class BackgroundJobRetryOptions
{
    public int MaxAttempts { get; set; } = 3;

    public int BaseDelaySeconds { get; set; } = 5;
}

public sealed class ProactiveMaintenanceOutreachOptions
{
    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 250;

    public int LastCompletedServiceAgeDays { get; set; } = 180;

    public int OdoDeltaThreshold { get; set; } = 5000;

    public int ForecastLookaheadDays { get; set; } = 30;

    public List<string> CompletedServiceStatuses { get; set; } = ["CLOSED", "READY", "COMPLETED", "DONE"];

    public List<string> ForecastTypes { get; set; } = ["MAINTENANCE", "SERVICE", "INSPECTION"];

    public string NotificationTitle { get; set; } = "Maintenance reminder";
}

public sealed class PostServiceSurveyOptions
{
    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 250;

    public int DelayHours { get; set; } = 24;

    public List<string> CompletedServiceStatuses { get; set; } = ["CLOSED", "READY", "COMPLETED", "DONE"];

    public string NotificationTitle { get; set; } = "How was your service experience?";
}

public sealed class InsuranceExpiryReminderOptions
{
    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 250;

    public int ReminderWindowDays { get; set; } = 30;

    public string NotificationTitle { get; set; } = "Insurance expiry reminder";
}

public sealed class ChatbotSessionCleanupOptions
{
    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 500;

    public int IdleToExpiredHours { get; set; } = 24;

    public int ExpiredToArchivedDays { get; set; } = 30;
}
