using System.Threading;
using System.Threading.Tasks;

namespace MyGarage.Interfaces;

public interface ICustomerAutomationJobProcessor
{
    Task<BackgroundJobRunResult> RunProactiveMaintenanceOutreachAsync(CancellationToken cancellationToken = default);

    Task<BackgroundJobRunResult> RunPostServiceSurveyAsync(CancellationToken cancellationToken = default);

    Task<BackgroundJobRunResult> RunInsuranceExpiryReminderAsync(CancellationToken cancellationToken = default);

    Task<BackgroundJobRunResult> RunChatbotSessionCleanupAsync(CancellationToken cancellationToken = default);
}

public sealed class BackgroundJobRunResult
{
    public string JobName { get; set; } = string.Empty;

    public int ExaminedCount { get; set; }

    public int NotifiedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    public bool Succeeded { get; set; } = true;

    public bool IsDisabled { get; set; }

    public string? ErrorMessage { get; set; }

    public static BackgroundJobRunResult Disabled(string jobName)
    {
        return new BackgroundJobRunResult
        {
            JobName = jobName,
            Succeeded = true,
            IsDisabled = true
        };
    }
}
